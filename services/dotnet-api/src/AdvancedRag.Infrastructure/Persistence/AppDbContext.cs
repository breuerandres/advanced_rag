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
    public DbSet<OrganizationalUnit> OrganizationalUnits => Set<OrganizationalUnit>();
    public DbSet<OrganizationalUnitClosure> OrganizationalUnitClosure => Set<OrganizationalUnitClosure>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<UserGroup> UserGroups => Set<UserGroup>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<DocumentPermission> DocumentPermissions => Set<DocumentPermission>();
    public DbSet<DocumentPermissionGroup> DocumentPermissionGroups => Set<DocumentPermissionGroup>();
    public DbSet<DocumentTag> DocumentTags => Set<DocumentTag>();
    public DbSet<DocumentImage> DocumentImages => Set<DocumentImage>();
    public DbSet<ReviewComment> ReviewComments => Set<ReviewComment>();
    public DbSet<ImportMetadata> ImportMetadata => Set<ImportMetadata>();
    public DbSet<UserGroupPublishGrant> UserGroupPublishGrants => Set<UserGroupPublishGrant>();
    public DbSet<ViewerSessionHandoffCode> ViewerSessionHandoffCodes => Set<ViewerSessionHandoffCode>();
    public DbSet<SessionHandoffCode> SessionHandoffCodes => Set<SessionHandoffCode>();
    public DbSet<UserAiBudgetLimit> UserAiBudgetLimits => Set<UserAiBudgetLimit>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<TenantConfig> TenantConfigs => Set<TenantConfig>();

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
            entity.Property(item => item.OrganizationalUnitId)
                .HasColumnName("organizational_unit_id")
                .HasDefaultValue(User.RootOrganizationalUnitId);
            entity.Property(item => item.AccessScopeVersion).HasColumnName("access_scope_version").HasDefaultValue(1L);
            entity.Property(item => item.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasOne<OrganizationalUnit>().WithMany().HasForeignKey(item => item.OrganizationalUnitId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => item.Email).IsUnique();
            entity.HasIndex(item => item.OrganizationalUnitId);
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

        modelBuilder.Entity<OrganizationalUnit>(entity =>
        {
            entity.ToTable("organizational_units", Schema);
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(item => item.ParentId).HasColumnName("parent_id");
            entity.Property(item => item.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(item => item.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(item => item.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasOne<OrganizationalUnit>().WithMany().HasForeignKey(item => item.ParentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => item.ParentId);
            entity.HasIndex(item => item.Name).IsUnique();
        });

        modelBuilder.Entity<OrganizationalUnitClosure>(entity =>
        {
            entity.ToTable("organizational_unit_closure", Schema);
            entity.HasKey(item => new { item.AncestorId, item.DescendantId });
            entity.Property(item => item.AncestorId).HasColumnName("ancestor_id");
            entity.Property(item => item.DescendantId).HasColumnName("descendant_id");
            entity.Property(item => item.Depth).HasColumnName("depth");
            entity.HasOne<OrganizationalUnit>().WithMany().HasForeignKey(item => item.AncestorId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<OrganizationalUnit>().WithMany().HasForeignKey(item => item.DescendantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => item.DescendantId);
        });

        modelBuilder.Entity<Group>(entity =>
        {
            entity.ToTable("groups", Schema);
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
            entity.Property(item => item.OwnerOrganizationalUnitId).HasColumnName("owner_organizational_unit_id");
            entity.Property(item => item.PublishingPolicy).HasColumnName("publishing_policy").HasMaxLength(32).HasDefaultValue("OwnerScope").IsRequired();
            entity.HasOne<OrganizationalUnit>().WithMany().HasForeignKey(item => item.OwnerOrganizationalUnitId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => item.Name).IsUnique();
            entity.HasIndex(item => item.OwnerOrganizationalUnitId);
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
            entity.Property(item => item.DocumentTypeId).HasColumnName("document_type_id");
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
            entity.HasOne<DocumentType>().WithMany().HasForeignKey(item => item.DocumentTypeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => item.DocumentTypeId);
            entity.HasIndex(item => new { item.DocumentId, item.VersionNumber }).IsUnique();
            entity.HasIndex(item => new { item.DocumentId, item.State });
        });

        modelBuilder.Entity<DocumentType>(entity =>
        {
            entity.ToTable("document_types", Schema);
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasColumnName("name").HasMaxLength(80).IsRequired();
            entity.Property(item => item.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(item => item.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);
            entity.Property(item => item.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(item => item.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasIndex(item => item.Name).IsUnique();
        });

        modelBuilder.Entity<DocumentPermission>(entity =>
        {
            entity.ToTable("document_permissions", Schema);
            entity.HasKey(item => item.Id);
            entity.Property(item => item.DocumentId).HasColumnName("document_id");
            entity.Property(item => item.OrganizationalUnitId).HasColumnName("organizational_unit_id");
            entity.Property(item => item.GroupId).HasColumnName("group_id");
            entity.Property(item => item.AttributeKey).HasColumnName("attribute_key").HasMaxLength(64);
            entity.Property(item => item.AttributeValue).HasColumnName("attribute_value").HasMaxLength(256);
            entity.Property(item => item.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasOne<Document>().WithMany().HasForeignKey(item => item.DocumentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<OrganizationalUnit>().WithMany().HasForeignKey(item => item.OrganizationalUnitId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Group>().WithMany().HasForeignKey(item => item.GroupId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => item.DocumentId);
            entity.HasIndex(item => item.OrganizationalUnitId);
            entity.HasIndex(item => item.GroupId);
            entity.HasIndex(item => new { item.AttributeKey, item.AttributeValue });
        });

        modelBuilder.Entity<DocumentPermissionGroup>(entity =>
        {
            entity.ToTable("document_permission_groups", Schema);
            entity.HasKey(item => new { item.DocumentPermissionId, item.GroupId });
            entity.Property(item => item.DocumentPermissionId).HasColumnName("document_permission_id");
            entity.Property(item => item.GroupId).HasColumnName("group_id");
            entity.HasOne<DocumentPermission>().WithMany().HasForeignKey(item => item.DocumentPermissionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Group>().WithMany().HasForeignKey(item => item.GroupId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => item.GroupId);
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

        modelBuilder.Entity<DocumentImage>(entity =>
        {
            entity.ToTable("document_images", Schema);
            entity.HasKey(item => item.Id);
            entity.Property(item => item.DocumentId).HasColumnName("document_id");
            entity.Property(item => item.ObjectKey).HasColumnName("object_key").HasMaxLength(512).IsRequired();
            entity.Property(item => item.OriginalFilename).HasColumnName("original_filename").HasMaxLength(260).IsRequired();
            entity.Property(item => item.ContentType).HasColumnName("content_type").HasMaxLength(120).IsRequired();
            entity.Property(item => item.SizeBytes).HasColumnName("size_bytes");
            entity.Property(item => item.Sha256Hash).HasColumnName("sha256_hash").HasMaxLength(64).IsRequired();
            entity.Property(item => item.AltText).HasColumnName("alt_text").HasMaxLength(500).IsRequired();
            entity.Property(item => item.UploadedByUserId).HasColumnName("uploaded_by_user_id");
            entity.Property(item => item.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasOne<Document>().WithMany().HasForeignKey(item => item.DocumentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => item.DocumentId);
            entity.HasIndex(item => item.ObjectKey).IsUnique();
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

        modelBuilder.Entity<UserGroupPublishGrant>(entity =>
        {
            entity.ToTable("user_group_publish_grants", Schema);
            entity.HasKey(item => new { item.UserId, item.GroupId });
            entity.Property(item => item.UserId).HasColumnName("user_id");
            entity.Property(item => item.GroupId).HasColumnName("group_id");
            entity.Property(item => item.GrantedByUserId).HasColumnName("granted_by_user_id");
            entity.Property(item => item.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Group>().WithMany().HasForeignKey(item => item.GroupId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.GrantedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => item.GroupId);
        });

        modelBuilder.Entity<ViewerSessionHandoffCode>(entity =>
        {
            entity.ToTable("viewer_session_handoff_codes", Schema);
            entity.HasKey(item => item.Id);
            entity.Property(item => item.CodeHash).HasColumnName("code_hash").HasMaxLength(64).IsRequired();
            entity.Property(item => item.UserId).HasColumnName("user_id");
            entity.Property(item => item.DocumentId).HasColumnName("document_id");
            entity.Property(item => item.Purpose).HasColumnName("purpose").HasMaxLength(32).IsRequired();
            entity.Property(item => item.AllowedStateScope).HasColumnName("allowed_state_scope").HasMaxLength(120).IsRequired();
            entity.Property(item => item.ExpiresAt).HasColumnName("expires_at");
            entity.Property(item => item.ConsumedAt).HasColumnName("consumed_at");
            entity.Property(item => item.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(item => item.RequestId).HasColumnName("request_id").HasMaxLength(128).IsRequired();
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Document>().WithMany().HasForeignKey(item => item.DocumentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => item.CodeHash).IsUnique();
            entity.HasIndex(item => item.ExpiresAt);
            entity.HasIndex(item => item.UserId);
            entity.HasIndex(item => item.DocumentId);
        });

        modelBuilder.Entity<SessionHandoffCode>(entity =>
        {
            entity.ToTable("session_handoff_codes", Schema);
            entity.HasKey(item => item.Id);
            entity.Property(item => item.CodeHash).HasColumnName("code_hash").HasMaxLength(64).IsRequired();
            entity.Property(item => item.UserId).HasColumnName("user_id");
            entity.Property(item => item.Target).HasColumnName("target").HasMaxLength(16).IsRequired();
            entity.Property(item => item.ExpiresAt).HasColumnName("expires_at");
            entity.Property(item => item.ConsumedAt).HasColumnName("consumed_at");
            entity.Property(item => item.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(item => item.RequestId).HasColumnName("request_id").HasMaxLength(128).IsRequired();
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => item.CodeHash).IsUnique();
            entity.HasIndex(item => item.ExpiresAt);
            entity.HasIndex(item => item.UserId);
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

        modelBuilder.Entity<TenantConfig>(entity =>
        {
            entity.ToTable("tenant_config", Schema);
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.BrandName).HasColumnName("brand_name").IsRequired();
            entity.Property(item => item.BrandLogoUrl).HasColumnName("brand_logo_url");
            entity.Property(item => item.BrandFaviconUrl).HasColumnName("brand_favicon_url");
            entity.Property(item => item.PrimaryColor).HasColumnName("primary_color").IsRequired();
            entity.Property(item => item.DefaultLocale).HasColumnName("default_locale").IsRequired();
            entity.Property(item => item.SupportedLocales).HasColumnName("supported_locales").IsRequired();
            entity.Property(item => item.LlmProvider).HasColumnName("llm_provider").IsRequired();
            entity.Property(item => item.LlmModel).HasColumnName("llm_model").IsRequired();
            entity.Property(item => item.LlmBaseUrl).HasColumnName("llm_base_url");
            entity.Property(item => item.EmbeddingProvider).HasColumnName("embedding_provider").IsRequired();
            entity.Property(item => item.EmbeddingModel).HasColumnName("embedding_model").IsRequired();
            entity.Property(item => item.EmbeddingDimensions).HasColumnName("embedding_dimensions");
            entity.Property(item => item.RerankerProvider).HasColumnName("reranker_provider").IsRequired();
            entity.Property(item => item.RerankerModel).HasColumnName("reranker_model").IsRequired();
            entity.Property(item => item.RerankerBaseUrl).HasColumnName("reranker_base_url");
            entity.Property(item => item.EnableBm25).HasColumnName("enable_bm25");
            entity.Property(item => item.EnableReranker).HasColumnName("enable_reranker");
            entity.Property(item => item.EnableConversationalMemory).HasColumnName("enable_conversational_memory");
            entity.Property(item => item.EnableQueryRewrite).HasColumnName("enable_query_rewrite");
            entity.Property(item => item.RagTopKVector).HasColumnName("rag_top_k_vector");
            entity.Property(item => item.RagTopKBm25).HasColumnName("rag_top_k_bm25");
            entity.Property(item => item.RagTopKFinal).HasColumnName("rag_top_k_final");
            entity.Property(item => item.RrfK).HasColumnName("rrf_k");
            entity.Property(item => item.ConversationHistoryTurns).HasColumnName("conversation_history_turns");
            entity.Property(item => item.CacheTtlHours).HasColumnName("cache_ttl_hours");
            entity.Property(item => item.CacheSimilarityThreshold).HasColumnName("cache_similarity_threshold").HasPrecision(4, 3);
            entity.Property(item => item.DefaultMonthlyBudgetUsd).HasColumnName("default_monthly_budget_usd").HasPrecision(8, 2);
            entity.Property(item => item.GlobalDailyBudgetUsd).HasColumnName("global_daily_budget_usd").HasPrecision(10, 2);
            entity.Property(item => item.EnableVlmImageDescription).HasColumnName("enable_vlm_image_description");
            entity.Property(item => item.EnableOtel).HasColumnName("enable_otel");
            entity.Property(item => item.S3Endpoint).HasColumnName("s3_endpoint");
            entity.Property(item => item.S3Bucket).HasColumnName("s3_bucket").IsRequired();
            entity.Property(item => item.S3Region).HasColumnName("s3_region").IsRequired();
            entity.Property(item => item.CustomerTimezone).HasColumnName("customer_timezone").IsRequired();
            entity.Property(item => item.ImportMaxFileSizeMb).HasColumnName("import_max_file_size_mb");
            entity.Property(item => item.ChatMaxQuestionChars).HasColumnName("chat_max_question_chars");
            entity.Property(item => item.SeededFromEnv).HasColumnName("seeded_from_env");
            entity.Property(item => item.CreatedAt).HasColumnName("created_at");
            entity.Property(item => item.UpdatedAt).HasColumnName("updated_at");
        });
    }
}
