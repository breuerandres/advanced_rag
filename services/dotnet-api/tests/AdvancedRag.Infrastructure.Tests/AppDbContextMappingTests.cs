using AdvancedRag.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AdvancedRag.Infrastructure.Tests;

public sealed class AppDbContextMappingTests
{
    private static readonly IReadOnlyDictionary<Type, string> ExpectedTables = new Dictionary<Type, string>
    {
        [typeof(User)] = "users",
        [typeof(Role)] = "roles",
        [typeof(UserRole)] = "user_roles",
        [typeof(Group)] = "groups",
        [typeof(UserGroup)] = "user_groups",
        [typeof(Document)] = "documents",
        [typeof(DocumentVersion)] = "document_versions",
        [typeof(DocumentPermission)] = "document_permissions",
        [typeof(DocumentTag)] = "document_tags",
        [typeof(DocumentImage)] = "document_images",
        [typeof(ReviewComment)] = "review_comments",
        [typeof(ImportMetadata)] = "import_metadata",
        [typeof(UserAiBudgetLimit)] = "user_ai_budget_limits",
        [typeof(AuditEvent)] = "audit_events",
        [typeof(ViewerSessionHandoffCode)] = "viewer_session_handoff_codes",
        [typeof(SessionHandoffCode)] = "session_handoff_codes",
        [typeof(TenantConfig)] = "tenant_config",
    };

    [Fact]
    public void Model_MapsEveryAppEntityToTheAppSchema()
    {
        using var db = CreateDbContext();

        foreach (var (clrType, tableName) in ExpectedTables)
        {
            var entityType = db.Model.FindEntityType(clrType);

            entityType.Should().NotBeNull();
            if (entityType is null)
            {
                throw new InvalidOperationException($"{clrType.Name} is not mapped.");
            }

            entityType.GetSchema().Should().Be("app");
            entityType.GetTableName().Should().Be(tableName);
        }
    }

    [Fact]
    public void Model_DoesNotMapWritableRagTables()
    {
        using var db = CreateDbContext();

        db.Model.GetEntityTypes()
            .Select(entity => entity.GetSchema())
            .Should()
            .OnlyContain(schema => schema == "app");
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=advanced_rag_test;Username=test;Password=test")
            .Options;

        return new AppDbContext(options);
    }
}
