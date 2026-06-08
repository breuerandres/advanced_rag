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
        [typeof(OrganizationalUnit)] = "organizational_units",
        [typeof(OrganizationalUnitClosure)] = "organizational_unit_closure",
        [typeof(Group)] = "groups",
        [typeof(UserGroup)] = "user_groups",
        [typeof(Document)] = "documents",
        [typeof(DocumentVersion)] = "document_versions",
        [typeof(DocumentPermission)] = "document_permissions",
        [typeof(DocumentPermissionGroup)] = "document_permission_groups",
        [typeof(DocumentTag)] = "document_tags",
        [typeof(DocumentImage)] = "document_images",
        [typeof(ReviewComment)] = "review_comments",
        [typeof(ImportMetadata)] = "import_metadata",
        [typeof(UserGroupPublishGrant)] = "user_group_publish_grants",
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

    [Fact]
    public void Model_MapsOrganizationalUnitsClosureAndAccessRules()
    {
        using var db = CreateDbContext();

        db.Model.FindEntityType(typeof(OrganizationalUnit))!
            .FindProperty(nameof(OrganizationalUnit.ParentId))!
            .GetColumnName()
            .Should()
            .Be("parent_id");
        db.Model.FindEntityType(typeof(OrganizationalUnitClosure))!
            .FindPrimaryKey()!
            .Properties.Select(property => property.Name)
            .Should()
            .Equal(nameof(OrganizationalUnitClosure.AncestorId), nameof(OrganizationalUnitClosure.DescendantId));
        db.Model.FindEntityType(typeof(User))!
            .FindProperty(nameof(User.OrganizationalUnitId))!
            .GetColumnName()
            .Should()
            .Be("organizational_unit_id");
        db.Model.FindEntityType(typeof(User))!
            .FindProperty(nameof(User.AccessScopeVersion))!
            .GetColumnName()
            .Should()
            .Be("access_scope_version");
        db.Model.FindEntityType(typeof(Group))!
            .FindProperty(nameof(Group.PublishingPolicy))!
            .GetColumnName()
            .Should()
            .Be("publishing_policy");
        db.Model.FindEntityType(typeof(DocumentPermission))!
            .FindProperty(nameof(DocumentPermission.OrganizationalUnitId))!
            .GetColumnName()
            .Should()
            .Be("organizational_unit_id");
        db.Model.FindEntityType(typeof(DocumentPermissionGroup))!
            .FindPrimaryKey()!
            .Properties.Select(property => property.Name)
            .Should()
            .Equal(nameof(DocumentPermissionGroup.DocumentPermissionId), nameof(DocumentPermissionGroup.GroupId));
        db.Model.FindEntityType(typeof(UserGroupPublishGrant))!
            .FindPrimaryKey()!
            .Properties.Select(property => property.Name)
            .Should()
            .Equal(nameof(UserGroupPublishGrant.UserId), nameof(UserGroupPublishGrant.GroupId));
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=advanced_rag_test;Username=test;Password=test")
            .Options;

        return new AppDbContext(options);
    }
}
