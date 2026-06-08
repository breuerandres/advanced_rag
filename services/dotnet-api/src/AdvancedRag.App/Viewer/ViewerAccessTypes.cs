using AdvancedRag.App.Documents;

namespace AdvancedRag.App.Viewer;

public interface IViewerAccessService
{
    Task<ViewerLinkResult> CreateLinkAsync(CreateViewerLinkCommand command, CancellationToken ct);

    Task<ViewerSessionHandoffResult> ConsumeHandoffAsync(ConsumeViewerSessionHandoffCommand command, CancellationToken ct);

    Task<ViewerDocumentResult> GetDocumentAsync(GetViewerDocumentCommand command, CancellationToken ct);
}

public interface IViewerAccessRepository
{
    Task<ViewerDocumentAccess?> FindDocumentAsync(Guid documentId, CancellationToken ct);
}

public interface IViewerSessionHandoffRepository
{
    Task StoreAsync(ViewerSessionHandoffRecord record, CancellationToken ct);

    Task<ViewerSessionHandoffRecord?> FindByCodeHashAsync(string codeHash, CancellationToken ct);

    Task MarkConsumedAsync(Guid id, DateTimeOffset consumedAt, CancellationToken ct);
}

public sealed record CreateViewerLinkCommand(
    Guid DocumentId,
    Guid UserId,
    IReadOnlyList<string> Roles,
    string Purpose,
    string? RequestId = null);

public sealed record ViewerLinkResult(string Url, DateTimeOffset ExpiresAt);

public sealed record ConsumeViewerSessionHandoffCommand(
    string HandoffCode,
    Guid DocumentId);

public sealed record ViewerSessionHandoffResult(
    Guid UserId,
    IReadOnlyList<string> AllowedStatuses,
    DateTimeOffset ExpiresAt);

public sealed record ViewerSessionHandoffRecord(
    Guid Id,
    string CodeHash,
    Guid UserId,
    Guid DocumentId,
    string Purpose,
    string AllowedStateScope,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? ConsumedAt,
    DateTimeOffset CreatedAt,
    string RequestId);

public sealed record GetViewerDocumentCommand(
    Guid DocumentId,
    Guid UserId,
    IReadOnlyList<string> Roles);

public sealed record ViewerDocumentAccess(
    Guid DocumentId,
    string Title,
    string State,
    ViewerDocumentVersion? DraftVersion,
    ViewerDocumentVersion? PublishedVersion,
    IReadOnlyList<DocumentAccessRuleRecord> AccessRules);

public sealed record ViewerDocumentVersion(
    Guid Id,
    int VersionNumber,
    string State,
    string Title,
    string DocumentType,
    string Audience,
    string ContentHtml);

public sealed record ViewerDocumentResult(
    Guid DocumentId,
    Guid DocumentVersionId,
    string Title,
    string State,
    string DocumentType,
    string Audience,
    string ContentHtml,
    DateTimeOffset TokenExpiresAt);

public sealed class ViewerAccessException : Exception
{
    public ViewerAccessException(
        string code,
        int httpStatus,
        string message,
        IReadOnlyDictionary<string, object?>? details = null)
        : base(message)
    {
        Code = code;
        HttpStatus = httpStatus;
        Details = details;
    }

    public string Code { get; }

    public int HttpStatus { get; }

    public IReadOnlyDictionary<string, object?>? Details { get; }
}
