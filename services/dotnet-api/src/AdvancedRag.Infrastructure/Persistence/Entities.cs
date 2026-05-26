namespace AdvancedRag.Infrastructure.Persistence;

public sealed class User
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public required string PasswordHash { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class Role
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
}

public sealed class UserRole
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
}

public sealed class Group
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
}

public sealed class UserGroup
{
    public Guid UserId { get; set; }
    public Guid GroupId { get; set; }
}

public sealed class Document
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public required string CurrentState { get; set; }
    public Guid? CurrentDraftVersionId { get; set; }
    public Guid? CurrentPublishedVersionId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class DocumentVersion
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public int VersionNumber { get; set; }
    public required string State { get; set; }
    public required string Title { get; set; }
    public required string DocumentType { get; set; }
    public required string Audience { get; set; }
    public required string ContentHtml { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SubmittedForReviewAt { get; set; }
    public Guid? SubmittedForReviewByUserId { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public Guid? PublishedByUserId { get; set; }
    public Guid? IndexingJobId { get; set; }
    public required string IndexingStatus { get; set; }
}

public sealed class DocumentPermission
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Guid? GroupId { get; set; }
    public string? AttributeKey { get; set; }
    public string? AttributeValue { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class DocumentTag
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public required string Name { get; set; }
}

public sealed class ReviewComment
{
    public Guid Id { get; set; }
    public Guid DocumentVersionId { get; set; }
    public Guid ActorUserId { get; set; }
    public required string Comment { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ImportMetadata
{
    public Guid Id { get; set; }
    public Guid DocumentVersionId { get; set; }
    public required string OriginalFilename { get; set; }
    public required string MimeType { get; set; }
    public long SizeBytes { get; set; }
    public required string Sha256Hash { get; set; }
    public Guid ImportedByUserId { get; set; }
    public required string ExtractionStatus { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class UserAiBudgetLimit
{
    public Guid UserId { get; set; }
    public decimal? MonthlyBudgetUsd { get; set; }
    public bool IsDisabled { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

public sealed class AuditEvent
{
    public Guid Id { get; set; }
    public Guid? ActorUserId { get; set; }
    public required string EventType { get; set; }
    public required string EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public required string DetailsJson { get; set; }
    public required string RequestId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
