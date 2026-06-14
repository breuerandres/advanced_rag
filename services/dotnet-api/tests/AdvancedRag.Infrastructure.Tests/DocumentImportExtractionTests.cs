using System.IO.Compression;
using AdvancedRag.App.Documents;
using AdvancedRag.Infrastructure.Documents;
using FluentAssertions;
using SkiaSharp;

namespace AdvancedRag.Infrastructure.Tests;

public sealed class DocumentImportExtractionTests
{
    private static readonly Guid ActorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DocumentId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private const string DocxMime =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private static DocumentImportExtractionService CreateService(ImportImageNormalizer? imageNormalizer = null)
    {
        return new DocumentImportExtractionService(
            new GanssDocumentHtmlSanitizer(),
            imageNormalizer ?? new ImportImageNormalizer());
    }

    [Fact]
    public async Task ExtractAsync_DocxReturnsTextHtmlAndSafeMetadata()
    {
        var service = CreateService();
        var bytes = CreateStructuredDocx();

        var result = await service.ExtractAsync(
            new ImportExtractionCommand(
                "policy.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                bytes,
            ActorId),
            CancellationToken.None);

        result.Text.Should().Contain("Safety policy");
        result.Text.Should().Contain("Use helmet");
        result.ContentHtml.Should().NotBeNullOrWhiteSpace();
        result.ContentHtml.Should().Contain("<h1>Safety policy</h1>");
        result.ContentHtml.Should().Contain("<strong>Use helmet</strong>");
        result.ContentHtml!.ToLowerInvariant().Should().NotContain("<img");
        result.ContentHtml.ToLowerInvariant().Should().NotContain("data:image");
        result.Metadata.OriginalFilename.Should().Be("policy.docx");
        result.Metadata.MimeType.Should().Be("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        result.Metadata.SizeBytes.Should().Be(bytes.Length);
        result.Metadata.Sha256Hash.Should().HaveLength(64);
        result.Metadata.ExtractionStatus.Should().Be("Extracted");
    }

    [Fact]
    public async Task ExtractAsync_PdfReturnsTextAndSafeMetadata()
    {
        var service = CreateService();
        var bytes = CreatePdfWithText("Texto de politica interna");

        var result = await service.ExtractAsync(
            new ImportExtractionCommand("policy.pdf", "application/pdf", bytes, ActorId),
            CancellationToken.None);

        result.Text.Should().Contain("Texto de politica interna");
        result.ContentHtml.Should().Be("<p>Texto de politica interna</p>");
        result.Metadata.OriginalFilename.Should().Be("policy.pdf");
        result.Metadata.MimeType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task ExtractAsync_RejectsFilesOverTenMegabytes()
    {
        var service = CreateService();
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
        var service = CreateService();
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
        var service = CreateService();

        var result = await service.ExtractAsync(
            new ImportExtractionCommand(
                "policy.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                CreateDocx("Unsaved import"),
                ActorId),
            CancellationToken.None);

        result.Metadata.DocumentVersionId.Should().BeNull();
    }

    [Fact]
    public async Task ExtractDocxWithImagesAsync_EmitsStableImageUrlAndReturnsImageContent()
    {
        var service = CreateService();

        var result = await service.ExtractDocxWithImagesAsync(
            new ImportExtractionCommand("policy.docx", DocxMime, CreateStructuredDocx(), ActorId),
            DocumentId,
            CancellationToken.None);

        result.Images.Should().HaveCount(1);
        ImportImageContent image = result.Images[0];
        result.ContentHtml.Should().Contain($"/api/document-images/{image.ImageId:D}/content");
        result.ContentHtml.ToLowerInvariant().Should().NotContain("data:image");
        image.ObjectKey.Should().StartWith($"documents/{DocumentId:D}/images/{image.ImageId:D}/");
        image.Sha256Hash.Should().HaveLength(64);
        image.Content.Should().NotBeEmpty();
        result.Text.Should().Contain("Safety policy");
    }

    [Fact]
    public async Task ExtractDocxWithImagesAsync_RejectsNonDocx()
    {
        var service = CreateService();

        var act = () => service.ExtractDocxWithImagesAsync(
            new ImportExtractionCommand("policy.pdf", "application/pdf", new byte[10], ActorId),
            DocumentId,
            CancellationToken.None);

        await act.Should().ThrowAsync<DocumentImportException>()
            .Where(error => error.Code == "VALIDATION_FAILED");
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

    private static byte[] CreateStructuredDocx()
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
                  <Default Extension="png" ContentType="image/png"/>
                  <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
                  <Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/>
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
            WriteZipEntry(
                archive,
                "word/_rels/document.xml.rels",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rIdStyles" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
                  <Relationship Id="rIdImage1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="media/image1.png"/>
                </Relationships>
                """);
            WriteZipEntry(
                archive,
                "word/styles.xml",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
                  <w:style w:type="paragraph" w:styleId="Heading1">
                    <w:name w:val="Heading 1"/>
                  </w:style>
                </w:styles>
                """);
            WriteZipEntry(
                archive,
                "word/document.xml",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <w:document
                  xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"
                  xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"
                  xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"
                  xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"
                  xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture">
                  <w:body>
                    <w:p>
                      <w:pPr><w:pStyle w:val="Heading1"/></w:pPr>
                      <w:r><w:t>Safety policy</w:t></w:r>
                    </w:p>
                    <w:p>
                      <w:r><w:rPr><w:b/></w:rPr><w:t>Use helmet</w:t></w:r>
                    </w:p>
                    <w:p>
                      <w:r>
                        <w:drawing>
                          <wp:inline>
                            <wp:docPr id="1" name="Embedded image" descr="Embedded image"/>
                            <a:graphic>
                              <a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture">
                                <pic:pic>
                                  <pic:blipFill>
                                    <a:blip r:embed="rIdImage1"/>
                                  </pic:blipFill>
                                </pic:pic>
                              </a:graphicData>
                            </a:graphic>
                          </wp:inline>
                        </w:drawing>
                      </w:r>
                    </w:p>
                  </w:body>
                </w:document>
                """);
            var image = archive.CreateEntry("word/media/image1.png");
            using var imageStream = image.Open();
            byte[] png = CreatePng(64, 64);
            imageStream.Write(png, 0, png.Length);
        }

        return output.ToArray();
    }

    private static byte[] CreatePng(int width, int height)
    {
        using SKBitmap bitmap = new(width, height);
        using (SKCanvas canvas = new(bitmap))
        {
            canvas.Clear(SKColors.CornflowerBlue);
        }

        using SKData data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
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
