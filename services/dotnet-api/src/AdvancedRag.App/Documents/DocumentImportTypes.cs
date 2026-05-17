namespace AdvancedRag.App.Documents;

public sealed record ImportExtractionCommand(
    string OriginalFilename,
    string MimeType,
    byte[] FileBytes,
    Guid ActorUserId);

public sealed record ImportExtractionMetadata(
    Guid? InstructionVersionId,
    string OriginalFilename,
    string MimeType,
    long SizeBytes,
    string Sha256Hash,
    string ExtractionStatus);

public sealed record ImportExtractionResult(string Text, ImportExtractionMetadata Metadata);

public interface IDocumentImportExtractionService
{
    Task<ImportExtractionResult> ExtractAsync(ImportExtractionCommand command, CancellationToken ct);
}

public sealed class DocumentImportException : Exception
{
    public DocumentImportException(
        string code,
        int httpStatus,
        string message,
        IReadOnlyDictionary<string, object?>? details = null)
        : base(message)
    {
        Code = code;
        HttpStatus = httpStatus;
        Details = details ?? new Dictionary<string, object?>();
    }

    public string Code { get; }

    public int HttpStatus { get; }

    public IReadOnlyDictionary<string, object?> Details { get; }
}
