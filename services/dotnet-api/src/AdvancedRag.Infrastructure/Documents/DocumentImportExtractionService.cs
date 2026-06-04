using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using AdvancedRag.App.Documents;
using Ganss.Xss;
using Mammoth;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace AdvancedRag.Infrastructure.Documents;

public sealed class DocumentImportExtractionService : IDocumentImportExtractionService
{
    public const int MaxImportBytes = 10 * 1024 * 1024;

    private const string DocxMimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string PdfMimeType = "application/pdf";
    private static readonly Regex ImageTagPattern = new(
        "<img\\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly HtmlSanitizer _htmlSanitizer = new();

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

        ImportedContent content = command.MimeType switch
        {
            DocxMimeType => ExtractDocx(command.FileBytes),
            PdfMimeType => ExtractPdf(command.FileBytes),
            _ => throw new DocumentImportException(
                "VALIDATION_FAILED",
                400,
                "Unsupported import file type.",
                new Dictionary<string, object?> { ["field"] = "mimeType" }),
        };

        var normalizedText = NormalizeExtractedText(content.Text);
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

        return Task.FromResult(new ImportExtractionResult(
            normalizedText,
            NormalizeContentHtml(content.ContentHtml),
            metadata));
    }

    private ImportedContent ExtractDocx(byte[] fileBytes)
    {
        var converter = new DocumentConverter()
            .ImageConverter(_ => new Dictionary<string, string>());

        using var htmlStream = new MemoryStream(fileBytes);
        var htmlResult = converter.ConvertToHtml(htmlStream);

        using var textStream = new MemoryStream(fileBytes);
        var textResult = converter.ExtractRawText(textStream);

        string sanitizedHtml = _htmlSanitizer.Sanitize(htmlResult.Value);
        sanitizedHtml = ImageTagPattern.Replace(sanitizedHtml, string.Empty);
        return new ImportedContent(textResult.Value, sanitizedHtml);
    }

    private static string ExtractPdfText(byte[] fileBytes)
    {
        using var stream = new MemoryStream(fileBytes);
        using var document = PdfDocument.Open(stream);
        var options = new ContentOrderTextExtractor.Options
        {
            SeparateParagraphsWithDoubleNewline = true,
            ReplaceWhitespaceWithSpace = true,
        };
        return string.Join(
            Environment.NewLine + Environment.NewLine,
            document.GetPages()
                .Select(page => ContentOrderTextExtractor.GetText(page, options))
                .Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    private static ImportedContent ExtractPdf(byte[] fileBytes)
    {
        string text = ExtractPdfText(fileBytes);
        return new ImportedContent(text, TextToDraftHtml(text));
    }

    private static string NormalizeExtractedText(string text)
    {
        return string.Join(
            Environment.NewLine,
            text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => line.Length > 0));
    }

    private static string? NormalizeContentHtml(string? html)
    {
        var trimmed = html?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string TextToDraftHtml(string text)
    {
        return string.Join(
            string.Empty,
            text.Split(
                    [Environment.NewLine + Environment.NewLine, "\r\n\r\n", "\n\n"],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(paragraph =>
                    $"<p>{HtmlEncoder.Default.Encode(paragraph).Replace("\r\n", "<br>", StringComparison.Ordinal).Replace("\n", "<br>", StringComparison.Ordinal)}</p>"));
    }

    private sealed record ImportedContent(string Text, string? ContentHtml);
}
