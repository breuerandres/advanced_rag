using AdvancedRag.App.Documents;
using AdvancedRag.Infrastructure.Documents;
using AdvancedRag.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace AdvancedRag.Infrastructure.Tests;

public sealed class EfDocumentRepositoryTests
{
    [Fact]
    public async Task FindAsync_LoadsAccessRulesFromPostgres()
    {
        await using var postgres = new PostgreSqlBuilder("pgvector/pgvector:pg16")
            .WithDatabase("advanced_rag_document_repository_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await postgres.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                postgres.GetConnectionString(),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", AppDbContext.Schema))
            .Options;

        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var groupId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var documentId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var versionId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var permissionId = Guid.Parse("55555555-5555-5555-5555-555555555555");

        await using (var db = new AppDbContext(options))
        {
            await db.Database.MigrateAsync();
            db.Users.Add(new User
            {
                Id = userId,
                Email = "admin@example.com",
                DisplayName = "Admin",
                PasswordHash = "hash",
                CreatedAt = DateTimeOffset.UtcNow,
            });
            db.Groups.Add(new Group { Id = groupId, Name = "Operations" });
            db.Documents.Add(new Document
            {
                Id = documentId,
                Title = "Safety policy",
                CurrentState = "In Review",
                CurrentDraftVersionId = versionId,
                CreatedByUserId = userId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            db.DocumentVersions.Add(new DocumentVersion
            {
                Id = versionId,
                DocumentId = documentId,
                VersionNumber = 1,
                State = "In Review",
                Title = "Safety policy",
                Audience = "All staff",
                ContentHtml = "<p>Use visible badge.</p>",
                CreatedAt = DateTimeOffset.UtcNow,
                IndexingStatus = "None",
            });
            db.DocumentPermissions.Add(new DocumentPermission
            {
                Id = permissionId,
                DocumentId = documentId,
                OrganizationalUnitId = User.RootOrganizationalUnitId,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            db.DocumentPermissionGroups.Add(new DocumentPermissionGroup
            {
                DocumentPermissionId = permissionId,
                GroupId = groupId,
            });
            await db.SaveChangesAsync();
        }

        await using (var db = new AppDbContext(options))
        {
            var repository = new EfDocumentRepository(db);

            var document = await repository.FindAsync(documentId, CancellationToken.None);

            document.Should().NotBeNull();
            document!.AccessRules.Should().ContainSingle();
            document.AccessRules.Single().OrganizationalUnitId.Should().Be(User.RootOrganizationalUnitId);
            document.AccessRules.Single().GroupIds.Should().Equal(groupId);
            document!.AllowedGroupIds.Should().Equal(groupId);
        }
    }

    [Fact]
    public async Task SaveAsync_TwiceOnSameContext_RewritingAccessRules_DoesNotThrowIdentityConflict()
    {
        // Reproduces the publish path: RequestPublishAsync calls SaveAsync twice on the
        // same scoped DbContext (Pending before indexing, Published after). The permission
        // rewrite deletes rows via ExecuteDelete (which does not evict tracked entities) and
        // re-adds DocumentPermission rows with the same ids, so the second save threw an EF
        // identity-map conflict that surfaced to the browser as HTTP 500 on publish.
        await using var postgres = new PostgreSqlBuilder("pgvector/pgvector:pg16")
            .WithDatabase("advanced_rag_document_repository_double_save_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await postgres.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                postgres.GetConnectionString(),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", AppDbContext.Schema))
            .Options;

        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var groupId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var documentId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var versionId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var ruleId = Guid.Parse("55555555-5555-5555-5555-555555555555");

        await using (var db = new AppDbContext(options))
        {
            await db.Database.MigrateAsync();
            db.Users.Add(new User
            {
                Id = userId,
                Email = "publisher@example.com",
                DisplayName = "Publisher",
                PasswordHash = "hash",
                CreatedAt = DateTimeOffset.UtcNow,
            });
            db.Groups.Add(new Group { Id = groupId, Name = "Operations" });
            await db.SaveChangesAsync();
        }

        var accessRules = new[] { new DocumentAccessRuleRecord(ruleId, null, new[] { groupId }) };

        DocumentVersionRecord Version(DocumentVersionState state, IndexingStatus indexing) => new(
            versionId,
            documentId,
            1,
            state,
            "Runbook",
            null,
            "Policy",
            "All staff",
            "<p>Keep your badge visible at all times.</p>",
            DateTimeOffset.UtcNow,
            null,
            null,
            null,
            null,
            indexing,
            null);

        var pending = new DocumentAggregate(
            documentId,
            "Runbook",
            DocumentState.InReview,
            Version(DocumentVersionState.InReview, IndexingStatus.Pending),
            null,
            accessRules,
            userId,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        var published = new DocumentAggregate(
            documentId,
            "Runbook",
            DocumentState.Published,
            null,
            Version(DocumentVersionState.Published, IndexingStatus.Succeeded),
            accessRules,
            userId,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        await using (var db = new AppDbContext(options))
        {
            var repository = new EfDocumentRepository(db);

            // One context instance for both saves, exactly like a single publish request.
            await repository.SaveAsync(
                pending,
                Array.Empty<ReviewCommentRecord>(),
                Array.Empty<DocumentAuditEvent>(),
                CancellationToken.None);

            Func<Task> secondSave = () => repository.SaveAsync(
                published,
                Array.Empty<ReviewCommentRecord>(),
                Array.Empty<DocumentAuditEvent>(),
                CancellationToken.None);

            await secondSave.Should().NotThrowAsync();
        }

        await using (var db = new AppDbContext(options))
        {
            var repository = new EfDocumentRepository(db);

            var document = await repository.FindAsync(documentId, CancellationToken.None);

            document.Should().NotBeNull();
            document!.State.Should().Be(DocumentState.Published);
            document.AccessRules.Should().ContainSingle();
            document.AccessRules.Single().GroupIds.Should().Equal(groupId);
        }
    }
}
