using AdvancedRag.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;

namespace AdvancedRag.Infrastructure.Tests;

public sealed class AppDbContextMigrationTests
{
    private static readonly string[] ExpectedAppTables =
    [
        "audit_events",
        "dimension_values",
        "dimensions",
        "document_dimension_values",
        "document_images",
        "groups",
        "import_metadata",
        "document_permissions",
        "document_tags",
        "document_versions",
        "documents",
        "review_comments",
        "roles",
        "tenant_config",
        "user_ai_budget_limits",
        "user_groups",
        "user_roles",
        "users",
        "viewer_session_handoff_codes",
    ];

    [Fact]
    public async Task EfMigration_CreatesOnlyAppSchemaTables()
    {
        await using var postgres = new PostgreSqlBuilder("pgvector/pgvector:pg16")
            .WithDatabase("advanced_rag_app_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await postgres.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                postgres.GetConnectionString(),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", AppDbContext.Schema))
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.ExecuteSqlRawAsync("create role rag_owner");
        await db.Database.MigrateAsync();

        var schemas = await db.Database
            .SqlQueryRaw<string>(
                """
                select schema_name::text as "Value"
                from information_schema.schemata
                where schema_name in ('app', 'rag')
                order by schema_name
                """)
            .ToListAsync();
        var appTables = await db.Database
            .SqlQueryRaw<string>(
                """
                select table_name::text as "Value"
                from information_schema.tables
                where table_schema = 'app'
                  and table_type = 'BASE TABLE'
                  and table_name <> '__EFMigrationsHistory'
                order by table_name
                """)
            .ToListAsync();
        var documentVersionColumns = await db.Database
            .SqlQueryRaw<string>(
                """
                select column_name::text as "Value"
                from information_schema.columns
                where table_schema = 'app'
                  and table_name = 'document_versions'
                order by column_name
                """)
            .ToListAsync();
        var ragOwnerPrivileges = await db.Database
            .SqlQueryRaw<string>(
                """
                select privilege::text as "Value"
                from (
                    values
                        (
                            'dimension_values',
                            has_table_privilege('rag_owner', 'app.dimension_values', 'SELECT')
                        ),
                        (
                            'dimensions',
                            has_table_privilege('rag_owner', 'app.dimensions', 'SELECT')
                        ),
                        (
                            'document_dimension_values',
                            has_table_privilege('rag_owner', 'app.document_dimension_values', 'SELECT')
                        ),
                        (
                            'document_permissions',
                            has_table_privilege('rag_owner', 'app.document_permissions', 'SELECT')
                        ),
                        (
                            'user_ai_budget_limits',
                            has_table_privilege('rag_owner', 'app.user_ai_budget_limits', 'SELECT')
                        )
                ) as checked_privileges(privilege, has_select)
                where has_select
                order by privilege
                """)
            .ToListAsync();

        schemas.Should().BeEquivalentTo(["app"]);
        appTables.Should().BeEquivalentTo(ExpectedAppTables);
        documentVersionColumns.Should().Contain("indexing_status");
        await AssertTenantConfigSingletonAsync(db);
        ragOwnerPrivileges.Should().BeEquivalentTo(
            [
                "dimension_values",
                "dimensions",
                "document_dimension_values",
                "document_permissions",
                "user_ai_budget_limits",
            ]);
    }

    private static async Task AssertTenantConfigSingletonAsync(AppDbContext db)
    {
        TenantConfigRow tenantConfig = await db.Database
            .SqlQueryRaw<TenantConfigRow>(
                """
                select
                    brand_name as "BrandName",
                    default_locale as "DefaultLocale",
                    supported_locales as "SupportedLocales",
                    llm_provider as "LlmProvider",
                    llm_model as "LlmModel",
                    embedding_provider as "EmbeddingProvider",
                    embedding_model as "EmbeddingModel",
                    embedding_dimensions as "EmbeddingDimensions",
                    enable_bm25 as "EnableBm25",
                    enable_reranker as "EnableReranker",
                    cache_ttl_hours as "CacheTtlHours",
                    cache_similarity_threshold as "CacheSimilarityThreshold",
                    default_monthly_budget_usd as "DefaultMonthlyBudgetUsd"
                from app.tenant_config
                """)
            .SingleAsync();

        tenantConfig.BrandName.Should().Be("Help Center");
        tenantConfig.DefaultLocale.Should().Be("es-AR");
        tenantConfig.SupportedLocales.Should().Equal("es-AR");
        tenantConfig.LlmProvider.Should().Be("openai");
        tenantConfig.LlmModel.Should().Be("gpt-4o-mini");
        tenantConfig.EmbeddingProvider.Should().Be("openai");
        tenantConfig.EmbeddingModel.Should().Be("text-embedding-3-large");
        tenantConfig.EmbeddingDimensions.Should().Be(1024);
        tenantConfig.EnableBm25.Should().BeTrue();
        tenantConfig.EnableReranker.Should().BeTrue();
        tenantConfig.CacheTtlHours.Should().Be(24);
        tenantConfig.CacheSimilarityThreshold.Should().Be(0.90m);
        tenantConfig.DefaultMonthlyBudgetUsd.Should().Be(5.00m);

        int singletonIndexes = await db.Database
            .SqlQueryRaw<int>(
                """
                select count(*)::int as "Value"
                from pg_indexes
                where schemaname = 'app'
                  and tablename = 'tenant_config'
                  and indexname = 'ux_tenant_config_singleton'
                """)
            .SingleAsync();
        singletonIndexes.Should().Be(1);
    }

    [Fact]
    public async Task EfMigration_SeedsDefaultAdminUser()
    {
        await using var postgres = new PostgreSqlBuilder("pgvector/pgvector:pg16")
            .WithDatabase("advanced_rag_default_admin_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await postgres.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                postgres.GetConnectionString(),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", AppDbContext.Schema))
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.ExecuteSqlRawAsync("create role rag_owner");
        await db.Database.MigrateAsync();

        DefaultAdminRow admin = await db.Database
            .SqlQueryRaw<DefaultAdminRow>(
                """
                select
                    users.email as "Email",
                    users.display_name as "DisplayName",
                    users.password_hash as "PasswordHash",
                    users.is_active as "IsActive",
                    roles.name as "RoleName",
                    budget.monthly_budget_usd as "MonthlyBudgetUsd",
                    budget.is_disabled as "BudgetIsDisabled"
                from app.users users
                join app.user_roles user_roles on user_roles.user_id = users."Id"
                join app.roles roles on roles."Id" = user_roles.role_id
                join app.user_ai_budget_limits budget on budget.user_id = users."Id"
                where users.email = 'admin@admin.com'
                """)
            .SingleAsync();

        admin.Email.Should().Be("admin@admin.com");
        admin.DisplayName.Should().Be("Default Admin");
        admin.IsActive.Should().BeTrue();
        admin.RoleName.Should().Be("Admin");
        admin.MonthlyBudgetUsd.Should().Be(5.00m);
        admin.BudgetIsDisabled.Should().BeFalse();
        admin.PasswordHash.Should().StartWith("pbkdf2-sha256$210000$");
    }

    [Fact]
    public async Task EfMigration_UpgradesLegacyInstructionVersionsBeforeDocumentRename()
    {
        await using var postgres = new PostgreSqlBuilder("pgvector/pgvector:pg16")
            .WithDatabase("advanced_rag_legacy_app_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await postgres.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                postgres.GetConnectionString(),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", AppDbContext.Schema))
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.ExecuteSqlRawAsync(
            """
            create schema app;
            create table app."__EFMigrationsHistory" (
                "MigrationId" character varying(150) not null,
                "ProductVersion" character varying(32) not null,
                constraint "PK___EFMigrationsHistory" primary key ("MigrationId")
            );
            insert into app."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            values ('20260513184201_InitialAppSchema', '8.0.27');
            create table app.instruction_versions (
                "Id" uuid not null,
                instruction_id uuid not null,
                instruction_type character varying(80) not null,
                constraint "PK_instruction_versions" primary key ("Id")
            );
            """);

        await db.Database.MigrateAsync();

        var legacyTableExists = await db.Database
            .SqlQueryRaw<bool>(
                """
                select to_regclass('app.instruction_versions') is not null as "Value"
                """)
            .SingleAsync();
        var documentVersionColumns = await db.Database
            .SqlQueryRaw<string>(
                """
                select column_name::text as "Value"
                from information_schema.columns
                where table_schema = 'app'
                  and table_name = 'document_versions'
                order by column_name
                """)
            .ToListAsync();

        legacyTableExists.Should().BeFalse();
        documentVersionColumns.Should().Contain(["document_id", "document_type", "indexing_status"]);
    }

    [Fact]
    public async Task EfMigration_RenamesLegacyAuditEventsToDocuments()
    {
        await using var postgres = new PostgreSqlBuilder("pgvector/pgvector:pg16")
            .WithDatabase("advanced_rag_legacy_audit_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await postgres.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                postgres.GetConnectionString(),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", AppDbContext.Schema))
            .Options;

        await using var db = new AppDbContext(options);
        IMigrator migrator = db.Database.GetService<IMigrator>();
        await migrator.MigrateAsync("20260520173000_GrantRagOwnerAppReadAccess");
        await db.Database.ExecuteSqlRawAsync(
            """
            insert into app.audit_events (
                "Id",
                actor_user_id,
                event_type,
                entity_type,
                entity_id,
                details_json,
                request_id,
                created_at
            )
            values (
                '99999999-9999-9999-9999-999999999999',
                null,
                'instruction.published',
                'instruction',
                '55555555-5555-5555-5555-555555555555',
                '{{"instructionId":"55555555-5555-5555-5555-555555555555"}}',
                'req-legacy-audit',
                now()
            );
            """);

        await migrator.MigrateAsync();

        LegacyAuditRow row = await db.Database
            .SqlQueryRaw<LegacyAuditRow>(
                """
                select
                    event_type as "EventType",
                    entity_type as "EntityType",
                    details_json::text as "DetailsJson"
                from app.audit_events
                where "Id" = '99999999-9999-9999-9999-999999999999'
                """)
            .SingleAsync();

        row.EventType.Should().Be("document.published");
        row.EntityType.Should().Be("document");
        row.DetailsJson.Should().Contain("documentId");
        row.DetailsJson.Should().NotContain("instructionId");
    }

    private sealed record LegacyAuditRow(
        string EventType,
        string EntityType,
        string DetailsJson);

    private sealed record DefaultAdminRow(
        string Email,
        string DisplayName,
        string PasswordHash,
        bool IsActive,
        string RoleName,
        decimal MonthlyBudgetUsd,
        bool BudgetIsDisabled);

    private sealed record TenantConfigRow(
        string BrandName,
        string DefaultLocale,
        string[] SupportedLocales,
        string LlmProvider,
        string LlmModel,
        string EmbeddingProvider,
        string EmbeddingModel,
        int EmbeddingDimensions,
        bool EnableBm25,
        bool EnableReranker,
        int CacheTtlHours,
        decimal CacheSimilarityThreshold,
        decimal DefaultMonthlyBudgetUsd);
}
