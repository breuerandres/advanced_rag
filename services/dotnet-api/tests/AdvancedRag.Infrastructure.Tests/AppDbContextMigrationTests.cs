using AdvancedRag.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace AdvancedRag.Infrastructure.Tests;

public sealed class AppDbContextMigrationTests
{
    private static readonly string[] ExpectedAppTables =
    [
        "audit_events",
        "groups",
        "import_metadata",
        "document_permissions",
        "document_tags",
        "document_versions",
        "documents",
        "review_comments",
        "roles",
        "user_ai_budget_limits",
        "user_groups",
        "user_roles",
        "users",
        "viewer_exchange_codes",
        "viewer_token_audit",
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
        ragOwnerPrivileges.Should().BeEquivalentTo(["document_permissions", "user_ai_budget_limits"]);
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
}
