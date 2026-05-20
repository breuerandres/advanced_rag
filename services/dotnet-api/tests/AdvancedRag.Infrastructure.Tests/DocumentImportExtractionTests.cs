using System.IO.Compression;
using AdvancedRag.App.Documents;
using AdvancedRag.Infrastructure.Documents;
using FluentAssertions;

namespace AdvancedRag.Infrastructure.Tests;

public sealed class DocumentImportExtractionTests
{
    private static readonly Guid ActorId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task ExtractAsync_DocxReturnsTextAndSafeMetadata()
    {
        var service = new DocumentImportExtractionService();
        var bytes = CreateDocx("Primera instruccion", "Segunda linea");

        var result = await service.ExtractAsync(
            new ImportExtractionCommand(
                "policy.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                bytes,
                ActorId),
            CancellationToken.None);

        result.Text.Should().Contain("Primera instruccion");
        result.Text.Should().Contain("Segunda linea");
        result.Metadata.OriginalFilename.Should().Be("policy.docx");
        result.Metadata.MimeType.Should().Be("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        result.Metadata.SizeBytes.Should().Be(bytes.Length);
        result.Metadata.Sha256Hash.Should().HaveLength(64);
        result.Metadata.ExtractionStatus.Should().Be("Extracted");
    }

    [Fact]
    public async Task ExtractAsync_PdfReturnsTextAndSafeMetadata()
    {
        var service = new DocumentImportExtractionService();
        var bytes = CreatePdfWithText("Texto de politica interna");

        var result = await service.ExtractAsync(
            new ImportExtractionCommand("policy.pdf", "application/pdf", bytes, ActorId),
            CancellationToken.None);

        result.Text.Should().Contain("Texto de politica interna");
        result.Metadata.OriginalFilename.Should().Be("policy.pdf");
        result.Metadata.MimeType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task ExtractAsync_RejectsFilesOverTenMegabytes()
    {
        var service = new DocumentImportExtractionService();
        var bytes = new byte[(10 * 1024 * 1024) + 1];

        var act = () => service.ExtractAsync(
            new ImportExtractionCommand("too-large.pdf", "application/pdf", bytes, ActorId),
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<DocumentImportException>()
            .Where(error => error.Code == "IMPORT_FILE_TOO_LARGE");
    }

    [Fact]
    public async Task ExtractAsync_RejectsDocxWithoutExtractableText()
    {
        var service = new DocumentImportExtractionService();
        var bytes = CreateDocx("   ");

        var act = () => service.ExtractAsync(
            new ImportExtractionCommand(
                "empty.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                bytes,
                ActorId),
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<DocumentImportException>()
            .Where(error => error.Code == "IMPORT_TEXT_NOT_EXTRACTABLE");
    }

    [Fact]
    public async Task ExtractAsync_DoesNotPersistUnsavedImportMetadata()
    {
        var service = new DocumentImportExtractionService();

        var result = await service.ExtractAsync(
            new ImportExtractionCommand(
                "policy.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                CreateDocx("Unsaved import"),
                ActorId),
            CancellationToken.None);

        result.Metadata.DocumentVersionId.Should().BeNull();
    }

    private static byte[] CreateDocx(params string[] paragraphs)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteZipEntry(
                archive,
                "[Content_Types].xml",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
                </Types>
                """);
            WriteZipEntry(
                archive,
                "_rels/.rels",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
                </Relationships>
                """);
            var body = string.Join(
                "",
                paragraphs.Select(text =>
                    $"<w:p><w:r><w:t>{System.Security.SecurityElement.Escape(text)}</w:t></w:r></w:p>"));
            WriteZipEntry(
                archive,
                "word/document.xml",
                $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
                  <w:body>{body}</w:body>
                </w:document>
                """);
        }

        return output.ToArray();
    }

    private static void WriteZipEntry(ZipArchive archive, string path, string text)
    {
        var entry = archive.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(text);
    }

    private static byte[] CreatePdfWithText(string text)
    {
        var escaped = text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("(", "\\(", StringComparison.Ordinal).Replace(")", "\\)", StringComparison.Ordinal);
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 144] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {44 + escaped.Length} >>\nstream\nBT /F1 12 Tf 72 72 Td ({escaped}) Tj ET\nendstream",
        };
        using var output = new MemoryStream();
        using var writer = new StreamWriter(output, leaveOpen: true);
        writer.WriteLine("%PDF-1.4");
        var offsets = new List<long> { 0 };
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(output.Position);
            writer.WriteLine($"{index + 1} 0 obj");
            writer.WriteLine(objects[index]);
            writer.WriteLine("endobj");
            writer.Flush();
        }

        var xref = output.Position;
        writer.WriteLine("xref");
        writer.WriteLine($"0 {objects.Length + 1}");
        writer.WriteLine("0000000000 65535 f ");
        foreach (var offset in offsets.Skip(1))
        {
            writer.WriteLine($"{offset:0000000000} 00000 n ");
        }

        writer.WriteLine("trailer");
        writer.WriteLine($"<< /Size {objects.Length + 1} /Root 1 0 R >>");
        writer.WriteLine("startxref");
        writer.WriteLine(xref);
        writer.WriteLine("%%EOF");
        writer.Flush();
        return output.ToArray();
    }
}
