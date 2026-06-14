using AdvancedRag.App.Auth;
using AdvancedRag.App.Documents;
using AdvancedRag.App.Viewer;

namespace AdvancedRag.App.DocumentImages;

public sealed record UploadDocumentImageCommand(
    Guid DocumentId,
    string FileName,
    string ContentType,
    long SizeBytes,
    Stream Content,
    string AltText,
    Guid ActorUserId);

public sealed record GetDocumentImageContentCommand(
    Guid ImageId,
    Guid UserId,
    IReadOnlyList<string> Roles);

public sealed record DocumentImageUploadResult(
    Guid ImageId,
    string Url,
    string AltText);

public sealed record DocumentImageContentResult(
    string FileName,
    string ContentType,
    long SizeBytes,
    Stream Content);

public sealed record DocumentImageRecord(
    Guid Id,
    Guid DocumentId,
    string ObjectKey,
    string OriginalFilename,
    string ContentType,
    long SizeBytes,
    string Sha256Hash,
    string AltText,
    Guid UploadedByUserId,
    DateTimeOffset CreatedAt);

public interface IDocumentImageService
{
    Task<DocumentImageUploadResult> UploadAsync(UploadDocumentImageCommand command, CancellationToken ct);

    Task<DocumentImageContentResult> GetContentAsync(GetDocumentImageContentCommand command, CancellationToken ct);
}

public interface IDocumentImageRepository
{
    Task AddAsync(DocumentImageRecord image, CancellationToken ct);

    Task<DocumentImageRecord?> FindAsync(Guid imageId, CancellationToken ct);
}

public interface IDocumentImageObjectStorage
{
    Task PutAsync(string objectKey, string contentType, Stream content, CancellationToken ct);

    Task<Stream> GetAsync(string objectKey, CancellationToken ct);
}

public sealed class DocumentImageException : Exception
{
    public DocumentImageException(
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

public sealed class DocumentImageService : IDocumentImageService
{
    private const long MaxImageSizeBytes = 5 * 1024 * 1024;

    private readonly IDocumentRepository _documents;
    private readonly IDocumentImageRepository _images;
    private readonly IDocumentImageObjectStorage _storage;
    private readonly IViewerAccessService _viewerAccess;
    private readonly TimeProvider _timeProvider;
    private readonly IEffectiveAccessScopeRepository? _accessScopes;
    private readonly IDocumentAccessPolicy? _accessPolicy;

    public DocumentImageService(
        IDocumentRepository documents,
        IDocumentImageRepository images,
        IDocumentImageObjectStorage storage,
        IViewerAccessService viewerAccess,
        TimeProvider? timeProvider = null,
        IEffectiveAccessScopeRepository? accessScopes = null,
        IDocumentAccessPolicy? accessPolicy = null)
    {
        _documents = documents;
        _images = images;
        _storage = storage;
        _viewerAccess = viewerAccess;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _accessScopes = accessScopes;
        _accessPolicy = accessPolicy;
    }

    public async Task<DocumentImageUploadResult> UploadAsync(UploadDocumentImageCommand command, CancellationToken ct)
    {
        DocumentAggregate document = await _documents.FindAsync(command.DocumentId, ct)
            ?? throw new DocumentImageException("NOT_FOUND", 404, "Document not found.");
        if (document.State == DocumentState.Archived)
        {
            throw new DocumentImageException("AUTH_FORBIDDEN", 403, "Cannot upload images to archived documents.");
        }

        await RequireCanManageDocumentAsync(command.ActorUserId, document.AccessRules, ct);

        string contentType = DocumentImageObjectKey.NormalizeContentType(command.ContentType);
        if (!DocumentImageObjectKey.TryGetExtension(contentType, out string? extension))
        {
            throw new DocumentImageException(
                "DOCUMENT_IMAGE_TYPE_UNSUPPORTED",
                415,
                "Document image type is unsupported.",
                new Dictionary<string, object?> { ["contentType"] = command.ContentType });
        }

        if (command.SizeBytes <= 0 || command.SizeBytes > MaxImageSizeBytes)
        {
            throw new DocumentImageException(
                "DOCUMENT_IMAGE_TOO_LARGE",
                413,
                "Document image exceeds the configured size limit.",
                new Dictionary<string, object?> { ["maxBytes"] = MaxImageSizeBytes });
        }

        Guid imageId = Guid.NewGuid();
        byte[] bytes;
        await using (MemoryStream buffer = new())
        {
            await command.Content.CopyToAsync(buffer, ct);
            bytes = buffer.ToArray();
        }

        string sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
        string objectKey = DocumentImageObjectKey.Build(command.DocumentId, imageId, sha256, extension!);
        await using MemoryStream uploadStream = new(bytes);
        await _storage.PutAsync(objectKey, contentType, uploadStream, ct);

        string altText = command.AltText.Trim();
        DocumentImageRecord image = new(
            imageId,
            command.DocumentId,
            objectKey,
            Path.GetFileName(command.FileName),
            contentType,
            bytes.LongLength,
            sha256,
            altText,
            command.ActorUserId,
            _timeProvider.GetUtcNow());
        await _images.AddAsync(image, ct);

        return new DocumentImageUploadResult(imageId, StableUrl(imageId), altText);
    }

    public async Task<DocumentImageContentResult> GetContentAsync(
        GetDocumentImageContentCommand command,
        CancellationToken ct)
    {
        DocumentImageRecord image = await _images.FindAsync(command.ImageId, ct)
            ?? throw new DocumentImageException("NOT_FOUND", 404, "Document image not found.");

        try
        {
            await _viewerAccess.GetDocumentAsync(
                new GetViewerDocumentCommand(image.DocumentId, command.UserId, command.Roles),
                ct);
        }
        catch (ViewerAccessException exception)
        {
            throw new DocumentImageException(exception.Code, exception.HttpStatus, exception.Message, exception.Details);
        }

        Stream content = await _storage.GetAsync(image.ObjectKey, ct);
        return new DocumentImageContentResult(image.OriginalFilename, image.ContentType, image.SizeBytes, content);
    }

    private static string StableUrl(Guid imageId)
    {
        return $"/api/document-images/{imageId:D}/content";
    }

    private async Task RequireCanManageDocumentAsync(
        Guid actorUserId,
        IReadOnlyList<DocumentAccessRuleRecord> rules,
        CancellationToken ct)
    {
        if (_accessPolicy is null)
        {
            return;
        }

        EffectiveAccessScope scope = _accessScopes is null
            ? throw new DocumentImageException("AUTH_FORBIDDEN", 403, "Actor cannot upload images to this document.")
            : await _accessScopes.FindForActiveUserAsync(actorUserId, ct)
                ?? throw new DocumentImageException("AUTH_FORBIDDEN", 403, "Actor cannot upload images to this document.");
        if (!await _accessPolicy.CanManageDraftAsync(scope, rules, ct))
        {
            throw new DocumentImageException("AUTH_FORBIDDEN", 403, "Actor cannot upload images to this document.");
        }
    }
}
