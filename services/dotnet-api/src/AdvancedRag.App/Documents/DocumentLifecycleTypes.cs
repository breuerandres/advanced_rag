namespace AdvancedRag.App.Documents;

public enum InstructionState
{
    Draft,
    InReview,
    Published,
    Archived,
}

public enum InstructionVersionState
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
    string InstructionType,
    string Audience,
    string ContentHtml,
    IReadOnlyList<Guid> AllowedGroupIds,
    Guid ActorUserId,
    string RequestId);

public sealed record UpdateDraftCommand(
    Guid InstructionId,
    string Title,
    string InstructionType,
    string Audience,
    string ContentHtml,
    IReadOnlyList<Guid> AllowedGroupIds,
    Guid ActorUserId,
    string RequestId);

public sealed record SendToReviewCommand(Guid InstructionId, string? Comment, Guid ActorUserId, string RequestId);

public sealed record ReturnToDraftCommand(Guid InstructionId, string Comment, Guid ActorUserId, string RequestId);

public sealed record RequestPublishCommand(
    Guid InstructionId,
    Guid ActorUserId,
    IReadOnlyList<string> ActorRoles,
    string RequestId);

public sealed record ArchiveInstructionCommand(
    Guid InstructionId,
    Guid ActorUserId,
    IReadOnlyList<string> ActorRoles,
    string RequestId);

public sealed record RestoreInstructionCommand(Guid InstructionId, Guid ActorUserId, string RequestId);

public sealed record DocumentVersionRecord(
    Guid Id,
    Guid InstructionId,
    int VersionNumber,
    InstructionVersionState State,
    string Title,
    string InstructionType,
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
    InstructionState State,
    DocumentVersionRecord? CurrentDraftVersion,
    DocumentVersionRecord? CurrentPublishedVersion,
    IReadOnlyList<Guid> AllowedGroupIds,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static DocumentAggregate NewDraft(
        Guid id,
        Guid versionId,
        string title,
        string instructionType,
        string audience,
        string contentHtml,
        IReadOnlyList<Guid> allowedGroupIds,
        Guid actorUserId)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new DocumentAggregate(
            id,
            title,
            InstructionState.Draft,
            new DocumentVersionRecord(
                versionId,
                id,
                1,
                InstructionVersionState.Draft,
                title,
                instructionType,
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
            allowedGroupIds,
            actorUserId,
            now,
            now);
    }
}

public sealed record DocumentSummary(
    Guid Id,
    string Title,
    InstructionState State,
    string InstructionType,
    string Audience,
    IReadOnlyList<Guid> AllowedGroupIds,
    int? DraftVersionNumber,
    int? PublishedVersionNumber,
    IndexingStatus IndexingStatus,
    DateTimeOffset UpdatedAt)
{
    public static DocumentSummary FromAggregate(DocumentAggregate document)
    {
        return new DocumentSummary(
            document.Id,
            document.Title,
            document.State,
            document.CurrentDraftVersion?.InstructionType
                ?? document.CurrentPublishedVersion?.InstructionType
                ?? string.Empty,
            document.CurrentDraftVersion?.Audience
                ?? document.CurrentPublishedVersion?.Audience
                ?? string.Empty,
            document.AllowedGroupIds,
            document.CurrentDraftVersion?.VersionNumber,
            document.CurrentPublishedVersion?.VersionNumber,
            document.CurrentDraftVersion?.IndexingStatus ?? IndexingStatus.None,
            document.UpdatedAt);
    }
}

public sealed record ReviewCommentRecord(
    Guid InstructionVersionId,
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

    Task<DocumentAggregate?> FindAsync(Guid instructionId, CancellationToken ct);

    Task SaveAsync(
        DocumentAggregate document,
        IReadOnlyList<ReviewCommentRecord> comments,
        IReadOnlyList<DocumentAuditEvent> auditEvents,
        CancellationToken ct);
}

public sealed record InternalIndexingRequest(
    Guid InstructionId,
    Guid InstructionVersionId,
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
