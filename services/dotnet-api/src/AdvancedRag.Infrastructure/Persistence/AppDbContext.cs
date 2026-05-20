using Microsoft.EntityFrameworkCore;

namespace AdvancedRag.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public const string Schema = "app";

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<UserGroup> UserGroups => Set<UserGroup>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<DocumentPermission> DocumentPermissions => Set<DocumentPermission>();
    public DbSet<DocumentTag> DocumentTags => Set<DocumentTag>();
    public DbSet<ReviewComment> ReviewComments => Set<ReviewComment>();
    public DbSet<ImportMetadata> ImportMetadata => Set<ImportMetadata>();
    public DbSet<ViewerExchangeCode> ViewerExchangeCodes => Set<ViewerExchangeCode>();
    public DbSet<ViewerTokenAudit> ViewerTokenAudit => Set<ViewerTokenAudit>();
    public DbSet<UserAiBudgetLimit> UserAiBudgetLimits => Set<UserAiBudgetLimit>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users", Schema);
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
            entity.Property(item => item.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
            entity.Property(item => item.PasswordHash).HasColumnName("password_hash").HasMaxLength(512).IsRequired();
            entity.Property(item => item.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(item => item.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasIndex(item => item.Email).IsUnique();
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles", Schema);
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasColumnName("name").HasMaxLength(64).IsRequired();
            entity.HasIndex(item => item.Name).IsUnique();
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("user_roles", Schema);
            entity.HasKey(item => new { item.UserId, item.RoleId });
            entity.Property(item => item.UserId).HasColumnName("user_id");
            entity.Property(item => item.RoleId).HasColumnName("role_id");
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Role>().WithMany().HasForeignKey(item => item.RoleId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Group>(entity =>
        {
            entity.ToTable("groups", Schema);
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
            entity.HasIndex(item => item.Name).IsUnique();
        });

        modelBuilder.Entity<UserGroup>(entity =>
        {
            entity.ToTable("user_groups", Schema);
            entity.HasKey(item => new { item.UserId, item.GroupId });
            entity.Property(item => item.UserId).HasColumnName("user_id");
            entity.Property(item => item.GroupId).HasColumnName("group_id");
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Group>().WithMany().HasForeignKey(item => item.GroupId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.ToTable("documents", Schema);
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Title).HasColumnName("title").HasMaxLength(240).IsRequired();
            entity.Property(item => item.CurrentState).HasColumnName("current_state").HasMaxLength(32).IsRequired();
            entity.Property(item => item.CurrentDraftVersionId).HasColumnName("current_draft_version_id");
            entity.Property(item => item.CurrentPublishedVersionId).HasColumnName("current_published_version_id");
            entity.Property(item => item.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(item => item.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(item => item.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => item.CurrentState);
        });

        modelBuilder.Entity<DocumentVersion>(entity =>
        {
            entity.ToTable("document_versions", Schema);
            entity.HasKey(item => item.Id);
            entity.Property(item => item.DocumentId).HasColumnName("document_id");
            entity.Property(item => item.VersionNumber).HasColumnName("version_number");
            entity.Property(item => item.State).HasColumnName("state").HasMaxLength(32).IsRequired();
            entity.Property(item => item.Title).HasColumnName("title").HasMaxLength(240).IsRequired();
            entity.Property(item => item.DocumentType).HasColumnName("document_type").HasMaxLength(80).IsRequired();
            entity.Property(item => item.Audience).HasColumnName("audience").HasMaxLength(160).IsRequired();
            entity.Property(item => item.ContentHtml).HasColumnName("content_html").IsRequired();
            entity.Property(item => item.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(item => item.SubmittedForReviewAt).HasColumnName("submitted_for_review_at");
            entity.Property(item => item.SubmittedForReviewByUserId).HasColumnName("submitted_for_review_by_user_id");
            entity.Property(item => item.PublishedAt).HasColumnName("published_at");
            entity.Property(item => item.PublishedByUserId).HasColumnName("published_by_user_id");
            entity.Property(item => item.IndexingJobId).HasColumnName("indexing_job_id");
            entity.Property(item => item.IndexingStatus).HasColumnName("indexing_status").HasMaxLength(32).HasDefaultValue("None").IsRequired();
            entity.HasOne<Document>().WithMany().HasForeignKey(item => item.DocumentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.SubmittedForReviewByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.PublishedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => new { item.DocumentId, item.VersionNumber }).IsUnique();
            entity.HasIndex(item => new { item.DocumentId, item.State });
        });

        modelBuilder.Entity<DocumentPermission>(entity =>
        {
            entity.ToTable("document_permissions", Schema);
            entity.HasKey(item => item.Id);
            entity.Property(item => item.DocumentId).HasColumnName("document_id");
            entity.Property(item => item.GroupId).HasColumnName("group_id");
            entity.Property(item => item.AttributeKey).HasColumnName("attribute_key").HasMaxLength(64);
            entity.Property(item => item.AttributeValue).HasColumnName("attribute_value").HasMaxLength(256);
            entity.Property(item => item.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasOne<Document>().WithMany().HasForeignKey(item => item.DocumentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Group>().WithMany().HasForeignKey(item => item.GroupId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => item.DocumentId);
            entity.HasIndex(item => item.GroupId);
            entity.HasIndex(item => new { item.AttributeKey, item.AttributeValue });
        });

        modelBuilder.Entity<DocumentTag>(entity =>
        {
            entity.ToTable("document_tags", Schema);
            entity.HasKey(item => item.Id);
            entity.Property(item => item.DocumentId).HasColumnName("document_id");
            entity.Property(item => item.Name).HasColumnName("name").HasMaxLength(80).IsRequired();
            entity.HasOne<Document>().WithMany().HasForeignKey(item => item.DocumentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => new { item.DocumentId, item.Name }).IsUnique();
        });

        modelBuilder.Entity<ReviewComment>(entity =>
        {
            entity.ToTable("review_comments", Schema);
            entity.HasKey(item => item.Id);
            entity.Property(item => item.DocumentVersionId).HasColumnName("document_version_id");
            entity.Property(item => item.ActorUserId).HasColumnName("actor_user_id");
            entity.Property(item => item.Comment).HasColumnName("comment").HasMaxLength(2000).IsRequired();
            entity.Property(item => item.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasOne<DocumentVersion>().WithMany().HasForeignKey(item => item.DocumentVersionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ImportMetadata>(entity =>
        {
            entity.ToTable("import_metadata", Schema);
            entity.HasKey(item => item.Id);
            entity.Property(item => item.DocumentVersionId).HasColumnName("document_version_id");
            entity.Property(item => item.OriginalFilename).HasColumnName("original_filename").HasMaxLength(260).IsRequired();
            entity.Property(item => item.MimeType).HasColumnName("mime_type").HasMaxLength(120).IsRequired();
            entity.Property(item => item.SizeBytes).HasColumnName("size_bytes");
            entity.Property(item => item.Sha256Hash).HasColumnName("sha256_hash").HasMaxLength(64).IsRequired();
            entity.Property(item => item.ImportedByUserId).HasColumnName("imported_by_user_id");
            entity.Property(item => item.ExtractionStatus).HasColumnName("extraction_status").HasMaxLength(32).IsRequired();
            entity.Property(item => item.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasOne<DocumentVersion>().WithMany().HasForeignKey(item => item.DocumentVersionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.ImportedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ViewerExchangeCode>(entity =>
        {
            entity.ToTable("viewer_exchange_codes", Schema);
            entity.HasKey(item => item.Id);
            entity.Property(item => item.CodeHash).HasColumnName("code_hash").HasMaxLength(128).IsRequired();
            entity.Property(item => item.DocumentId).HasColumnName("document_id");
            entity.Property(item => item.UserId).HasColumnName("user_id");
            entity.Property(item => item.Purpose).HasColumnName("purpose").HasMaxLength(64).IsRequired();
            entity.Property(item => item.AllowedStatuses).HasColumnName("allowed_statuses").HasMaxLength(160).IsRequired();
            entity.Property(item => item.ExpiresAt).HasColumnName("expires_at");
            entity.Property(item => item.ConsumedAt).HasColumnName("consumed_at");
            entity.Property(item => item.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasOne<Document>().WithMany().HasForeignKey(item => item.DocumentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => item.CodeHash).IsUnique();
            entity.HasIndex(item => item.ExpiresAt);
        });

        modelBuilder.Entity<ViewerTokenAudit>(entity =>
        {
            entity.ToTable("viewer_token_audit", Schema);
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ViewerTokenId).HasColumnName("viewer_token_id").HasMaxLength(128).IsRequired();
            entity.Property(item => item.DocumentId).HasColumnName("document_id");
            entity.Property(item => item.UserId).HasColumnName("user_id");
            entity.Property(item => item.Purpose).HasColumnName("purpose").HasMaxLength(64).IsRequired();
            entity.Property(item => item.IssuedAt).HasColumnName("issued_at").HasDefaultValueSql("now()");
            entity.Property(item => item.ExpiresAt).HasColumnName("expires_at");
            entity.HasOne<Document>().WithMany().HasForeignKey(item => item.DocumentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => item.ViewerTokenId).IsUnique();
        });

        modelBuilder.Entity<UserAiBudgetLimit>(entity =>
        {
            entity.ToTable("user_ai_budget_limits", Schema);
            entity.HasKey(item => item.UserId);
            entity.Property(item => item.UserId).HasColumnName("user_id");
            entity.Property(item => item.MonthlyBudgetUsd).HasColumnName("monthly_budget_usd").HasPrecision(12, 4);
            entity.Property(item => item.IsDisabled).HasColumnName("is_disabled").HasDefaultValue(false);
            entity.Property(item => item.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.Property(item => item.UpdatedByUserId).HasColumnName("updated_by_user_id");
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.ToTable("audit_events", Schema);
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ActorUserId).HasColumnName("actor_user_id");
            entity.Property(item => item.EventType).HasColumnName("event_type").HasMaxLength(120).IsRequired();
            entity.Property(item => item.EntityType).HasColumnName("entity_type").HasMaxLength(120).IsRequired();
            entity.Property(item => item.EntityId).HasColumnName("entity_id");
            entity.Property(item => item.DetailsJson).HasColumnName("details_json").HasColumnType("jsonb").IsRequired();
            entity.Property(item => item.RequestId).HasColumnName("request_id").HasMaxLength(128).IsRequired();
            entity.Property(item => item.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => item.CreatedAt);
            entity.HasIndex(item => item.ActorUserId);
            entity.HasIndex(item => item.EventType);
        });
    }
}
