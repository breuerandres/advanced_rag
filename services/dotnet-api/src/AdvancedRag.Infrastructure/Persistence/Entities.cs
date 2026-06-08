namespace AdvancedRag.Infrastructure.Persistence;

public sealed class User
{
    public static readonly Guid RootOrganizationalUnitId = Guid.Parse("01000000-0000-0000-0000-000000000001");

    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public required string PasswordHash { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid OrganizationalUnitId { get; set; } = RootOrganizationalUnitId;
    public long AccessScopeVersion { get; set; } = 1;
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

public sealed class OrganizationalUnit
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public Guid? ParentId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class OrganizationalUnitClosure
{
    public Guid AncestorId { get; set; }
    public Guid DescendantId { get; set; }
    public int Depth { get; set; }
}

public sealed class Group
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public Guid? OwnerOrganizationalUnitId { get; set; }
    public string PublishingPolicy { get; set; } = "OwnerScope";
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
    public Guid? OrganizationalUnitId { get; set; }
    public Guid? GroupId { get; set; }
    public string? AttributeKey { get; set; }
    public string? AttributeValue { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class DocumentPermissionGroup
{
    public Guid DocumentPermissionId { get; set; }
    public Guid GroupId { get; set; }
}

public sealed class DocumentTag
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public required string Name { get; set; }
}

public sealed class DocumentImage
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public required string ObjectKey { get; set; }
    public required string OriginalFilename { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public required string Sha256Hash { get; set; }
    public required string AltText { get; set; }
    public Guid UploadedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
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

public sealed class UserGroupPublishGrant
{
    public Guid UserId { get; set; }
    public Guid GroupId { get; set; }
    public Guid GrantedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ViewerSessionHandoffCode
{
    public Guid Id { get; set; }
    public required string CodeHash { get; set; }
    public Guid UserId { get; set; }
    public Guid DocumentId { get; set; }
    public required string Purpose { get; set; }
    public required string AllowedStateScope { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public required string RequestId { get; set; }
}

public sealed class SessionHandoffCode
{
    public Guid Id { get; set; }
    public required string CodeHash { get; set; }
    public Guid UserId { get; set; }
    public required string Target { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public required string RequestId { get; set; }
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

public sealed class TenantConfig
{
    public Guid Id { get; set; }
    public required string BrandName { get; set; }
    public string? BrandLogoUrl { get; set; }
    public string? BrandFaviconUrl { get; set; }
    public required string PrimaryColor { get; set; }
    public required string DefaultLocale { get; set; }
    public required string[] SupportedLocales { get; set; }
    public required string LlmProvider { get; set; }
    public required string LlmModel { get; set; }
    public string? LlmBaseUrl { get; set; }
    public required string EmbeddingProvider { get; set; }
    public required string EmbeddingModel { get; set; }
    public int EmbeddingDimensions { get; set; }
    public required string RerankerProvider { get; set; }
    public required string RerankerModel { get; set; }
    public string? RerankerBaseUrl { get; set; }
    public bool EnableBm25 { get; set; }
    public bool EnableReranker { get; set; }
    public bool EnableConversationalMemory { get; set; }
    public bool EnableQueryRewrite { get; set; }
    public int RagTopKVector { get; set; }
    public int RagTopKBm25 { get; set; }
    public int RagTopKFinal { get; set; }
    public int RrfK { get; set; }
    public int ConversationHistoryTurns { get; set; }
    public int CacheTtlHours { get; set; }
    public decimal CacheSimilarityThreshold { get; set; }
    public decimal DefaultMonthlyBudgetUsd { get; set; }
    public decimal? GlobalDailyBudgetUsd { get; set; }
    public bool EnableVlmImageDescription { get; set; }
    public bool EnableOtel { get; set; }
    public string? S3Endpoint { get; set; }
    public required string S3Bucket { get; set; }
    public required string S3Region { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
