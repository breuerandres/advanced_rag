using System.Security.Cryptography;
using AdvancedRag.App.Documents;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;

namespace AdvancedRag.Infrastructure.Documents;

public sealed class DocumentImportExtractionService : IDocumentImportExtractionService
{
    public const int MaxImportBytes = 10 * 1024 * 1024;

    private const string DocxMimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string PdfMimeType = "application/pdf";

    public Task<ImportExtractionResult> ExtractAsync(ImportExtractionCommand command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (command.FileBytes.LongLength > MaxImportBytes)
        {
            throw new DocumentImportException(
                "IMPORT_FILE_TOO_LARGE",
                413,
                "Uploaded import file exceeds the configured size limit.",
                new Dictionary<string, object?> { ["maxBytes"] = MaxImportBytes });
        }

        var text = command.MimeType switch
        {
            DocxMimeType => ExtractDocx(command.FileBytes),
            PdfMimeType => ExtractPdf(command.FileBytes),
            _ => throw new DocumentImportException(
                "VALIDATION_FAILED",
                400,
                "Unsupported import file type.",
                new Dictionary<string, object?> { ["field"] = "mimeType" }),
        };

        var normalizedText = NormalizeExtractedText(text);
        if (normalizedText.Length == 0)
        {
            throw new DocumentImportException(
                "IMPORT_TEXT_NOT_EXTRACTABLE",
                422,
                "Uploaded file has no extractable text.");
        }

        var metadata = new ImportExtractionMetadata(
            null,
            Path.GetFileName(command.OriginalFilename),
            command.MimeType,
            command.FileBytes.LongLength,
            Convert.ToHexString(SHA256.HashData(command.FileBytes)).ToLowerInvariant(),
            "Extracted");

        return Task.FromResult(new ImportExtractionResult(normalizedText, metadata));
    }

    private static string ExtractDocx(byte[] fileBytes)
    {
        using var stream = new MemoryStream(fileBytes);
        using var document = WordprocessingDocument.Open(stream, false);
        var body = document.MainDocumentPart?.Document?.Body;
        return body?.InnerText ?? string.Empty;
    }

    private static string ExtractPdf(byte[] fileBytes)
    {
        using var stream = new MemoryStream(fileBytes);
        using var document = PdfDocument.Open(stream);
        return string.Join(Environment.NewLine, document.GetPages().Select(page => page.Text));
    }

    private static string NormalizeExtractedText(string text)
    {
        return string.Join(
            Environment.NewLine,
            text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => line.Length > 0));
    }
}
