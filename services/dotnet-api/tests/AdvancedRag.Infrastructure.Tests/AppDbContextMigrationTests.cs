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
        "instruction_permissions",
        "instruction_tags",
        "instruction_versions",
        "instructions",
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

        schemas.Should().BeEquivalentTo(["app"]);
        appTables.Should().BeEquivalentTo(ExpectedAppTables);
    }
}
