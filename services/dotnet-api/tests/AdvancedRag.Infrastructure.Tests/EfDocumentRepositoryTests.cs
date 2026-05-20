using AdvancedRag.Infrastructure.Documents;
using AdvancedRag.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace AdvancedRag.Infrastructure.Tests;

public sealed class EfDocumentRepositoryTests
{
    [Fact]
    public async Task FindAsync_LoadsAllowedGroupIdsFromPostgres()
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
                DocumentType = "Policy",
                Audience = "All staff",
                ContentHtml = "<p>Use visible badge.</p>",
                CreatedAt = DateTimeOffset.UtcNow,
                IndexingStatus = "None",
            });
            db.DocumentPermissions.Add(new DocumentPermission
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                GroupId = groupId,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        await using (var db = new AppDbContext(options))
        {
            var repository = new EfDocumentRepository(db);

            var document = await repository.FindAsync(documentId, CancellationToken.None);

            document.Should().NotBeNull();
            document!.AllowedGroupIds.Should().Equal(groupId);
        }
    }
}
