using AdvancedRag.App.DocumentImages;

namespace AdvancedRag.App.Documents;

public sealed class DocumentImportService : IDocumentImportService
{
    private const string DefaultTitle = "Imported document";

    private readonly IDocumentImportExtractionService _extraction;
    private readonly IDocumentRepository _documents;
    private readonly IDocumentImageObjectStorage _storage;
    private readonly IDocumentImageRepository _images;
    private readonly TimeProvider _timeProvider;

    public DocumentImportService(
        IDocumentImportExtractionService extraction,
        IDocumentRepository documents,
        IDocumentImageObjectStorage storage,
        IDocumentImageRepository images,
        TimeProvider? timeProvider = null)
    {
        _extraction = extraction;
        _documents = documents;
        _storage = storage;
        _images = images;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<DocumentAggregate> ImportDocxAsync(ImportDocxCommand command, CancellationToken ct)
    {
        Guid documentId = Guid.NewGuid();
        DocxImportExtractionResult extracted = await _extraction.ExtractDocxWithImagesAsync(
            new ImportExtractionCommand(command.OriginalFilename, command.MimeType, command.FileBytes, command.ActorUserId),
            documentId,
            ct);

        string title = TitleFromFilename(command.OriginalFilename);
        DocumentAggregate draft = DocumentAggregate.NewDraft(
            documentId,
            Guid.NewGuid(),
            title,
            null,
            string.Empty,
            extracted.ContentHtml,
            Array.Empty<DocumentAccessRuleRecord>(),
            command.ActorUserId);

        await _documents.SaveAsync(
            draft,
            Array.Empty<ReviewCommentRecord>(),
            new[]
            {
                new DocumentAuditEvent(
                    command.ActorUserId,
                    "document.imported",
                    documentId,
                    command.RequestId,
                    new Dictionary<string, object?>
                    {
                        ["documentId"] = documentId,
                        ["originalFilename"] = Path.GetFileName(command.OriginalFilename),
                        ["imageCount"] = extracted.Images.Count,
                    }),
            },
            ct);

        DateTimeOffset now = _timeProvider.GetUtcNow();
        foreach (ImportImageContent image in extracted.Images)
        {
            await using MemoryStream stream = new(image.Content);
            await _storage.PutAsync(image.ObjectKey, image.ContentType, stream, ct);
            await _images.AddAsync(
                new DocumentImageRecord(
                    image.ImageId,
                    documentId,
                    image.ObjectKey,
                    Path.GetFileName(command.OriginalFilename),
                    image.ContentType,
                    image.SizeBytes,
                    image.Sha256Hash,
                    image.AltText,
                    command.ActorUserId,
                    now),
                ct);
        }

        return draft;
    }

    private static string TitleFromFilename(string filename)
    {
        string title = Path.GetFileNameWithoutExtension(filename).Trim();
        return title.Length == 0 ? DefaultTitle : title;
    }
}
