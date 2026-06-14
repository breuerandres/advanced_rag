namespace AdvancedRag.App.Documents;

public enum DocumentState
{
    Draft,
    InReview,
    Published,
    Archived,
}

public enum DocumentVersionState
{
    Draft,
    InReview,
    Published,
    Archived,
}

public enum IndexingStatus
{
    None,
    Pending,
    Succeeded,
    Failed,
}

public sealed record CreateDocumentCommand(
    string Title,
    Guid? DocumentTypeId,
    string Audience,
    string ContentHtml,
    IReadOnlyList<DocumentAccessRuleDraft> AccessRules,
    Guid ActorUserId,
    string RequestId);

public sealed record UpdateDraftCommand(
    Guid DocumentId,
    string Title,
    Guid? DocumentTypeId,
    string Audience,
    string ContentHtml,
    IReadOnlyList<DocumentAccessRuleDraft> AccessRules,
    Guid ActorUserId,
    string RequestId);

public sealed record SendToReviewCommand(Guid DocumentId, string? Comment, Guid ActorUserId, string RequestId);

public sealed record ReturnToDraftCommand(Guid DocumentId, string Comment, Guid ActorUserId, string RequestId);

public sealed record RequestPublishCommand(
    Guid DocumentId,
    Guid ActorUserId,
    IReadOnlyList<string> ActorRoles,
    string RequestId);

public sealed record ArchiveDocumentCommand(
    Guid DocumentId,
    Guid ActorUserId,
    IReadOnlyList<string> ActorRoles,
    string RequestId);

public sealed record RestoreDocumentCommand(Guid DocumentId, Guid ActorUserId, string RequestId);

public sealed record DocumentAccessRuleDraft(
    Guid? OrganizationalUnitId,
    IReadOnlyList<Guid> GroupIds);

public sealed record DocumentAccessRuleRecord(
    Guid Id,
    Guid? OrganizationalUnitId,
    IReadOnlyList<Guid> GroupIds);

public sealed record DocumentVersionRecord(
    Guid Id,
    Guid DocumentId,
    int VersionNumber,
    DocumentVersionState State,
    string Title,
    Guid? DocumentTypeId,
    string DocumentType,
    string Audience,
    string ContentHtml,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SubmittedForReviewAt,
    Guid? SubmittedForReviewByUserId,
    DateTimeOffset? PublishedAt,
    Guid? PublishedByUserId,
    IndexingStatus IndexingStatus,
    Guid? IndexingJobId);

public sealed record DocumentAggregate(
    Guid Id,
    string Title,
    DocumentState State,
    DocumentVersionRecord? CurrentDraftVersion,
    DocumentVersionRecord? CurrentPublishedVersion,
    IReadOnlyList<DocumentAccessRuleRecord> AccessRules,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public IReadOnlyList<Guid> AllowedGroupIds => AccessRules
        .SelectMany(rule => rule.GroupIds)
        .Distinct()
        .Order()
        .ToArray();

    public static DocumentAggregate NewDraft(
        Guid id,
        Guid versionId,
        string title,
        Guid? documentTypeId,
        string documentTypeName,
        string audience,
        string contentHtml,
        IReadOnlyList<DocumentAccessRuleRecord> accessRules,
        Guid actorUserId)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new DocumentAggregate(
            id,
            title,
            DocumentState.Draft,
            new DocumentVersionRecord(
                versionId,
                id,
                1,
                DocumentVersionState.Draft,
                title,
                documentTypeId,
                documentTypeName,
                audience,
                contentHtml,
                now,
                null,
                null,
                null,
                null,
                IndexingStatus.None,
                null),
            null,
            accessRules,
            actorUserId,
            now,
            now);
    }
}

public sealed record DocumentSummary(
    Guid Id,
    string Title,
    DocumentState State,
    Guid? DocumentTypeId,
    string DocumentType,
    string Audience,
    IReadOnlyList<DocumentAccessRuleRecord> AccessRules,
    int? DraftVersionNumber,
    int? PublishedVersionNumber,
    IndexingStatus IndexingStatus,
    DateTimeOffset UpdatedAt)
{
    public IReadOnlyList<Guid> AllowedGroupIds => AccessRules
        .SelectMany(rule => rule.GroupIds)
        .Distinct()
        .Order()
        .ToArray();

    public static DocumentSummary FromAggregate(DocumentAggregate document)
    {
        return new DocumentSummary(
            document.Id,
            document.Title,
            document.State,
            document.CurrentDraftVersion?.DocumentTypeId
                ?? document.CurrentPublishedVersion?.DocumentTypeId,
            document.CurrentDraftVersion?.DocumentType
                ?? document.CurrentPublishedVersion?.DocumentType
                ?? string.Empty,
            document.CurrentDraftVersion?.Audience
                ?? document.CurrentPublishedVersion?.Audience
                ?? string.Empty,
            document.AccessRules,
            document.CurrentDraftVersion?.VersionNumber,
            document.CurrentPublishedVersion?.VersionNumber,
            document.CurrentDraftVersion?.IndexingStatus ?? IndexingStatus.None,
            document.UpdatedAt);
    }
}

public sealed record ReviewCommentRecord(
    Guid DocumentVersionId,
    Guid ActorUserId,
    string Comment);

public sealed record DocumentAuditEvent(
    Guid? ActorUserId,
    string EventType,
    Guid EntityId,
    string RequestId,
    IReadOnlyDictionary<string, object?> Details);

public interface IDocumentRepository
{
    Task<IReadOnlyList<DocumentSummary>> ListAsync(CancellationToken ct);

    Task<DocumentAggregate?> FindAsync(Guid documentId, CancellationToken ct);

    Task SaveAsync(
        DocumentAggregate document,
        IReadOnlyList<ReviewCommentRecord> comments,
        IReadOnlyList<DocumentAuditEvent> auditEvents,
        CancellationToken ct);
}

public sealed record InternalIndexingRequest(
    Guid DocumentId,
    Guid DocumentVersionId,
    string ContentHtml,
    string CorpusMode,
    bool Retry);

public sealed record InternalIndexingResult(
    Guid JobId,
    string Status,
    int ChunkCount,
    string? ErrorCode,
    string? ErrorMessage);

public interface IInternalIndexingClient
{
    Task<InternalIndexingResult> CreateIndexingJobAsync(
        InternalIndexingRequest request,
        CancellationToken ct);
}

public interface IInternalCacheInvalidationClient
{
    /// <summary>Best-effort invalidation of rag semantic-cache entries sourced from the given documents.</summary>
    Task<int> InvalidateDocumentsAsync(IReadOnlyList<Guid> documentIds, CancellationToken ct);
}

public sealed class DocumentLifecycleException : Exception
{
    public DocumentLifecycleException(
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
