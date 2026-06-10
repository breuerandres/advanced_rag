using AdvancedRag.Infrastructure.Persistence;
using AdvancedRag.Infrastructure.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace AdvancedRag.Infrastructure.Tests;

public sealed class EfUserAdministrationRepositoryTests
{
    [Fact]
    public async Task SetUserOrganizationalUnitAsync_ReassignsUnitAndBumpsAccessScopeVersion()
    {
        await using var postgres = new PostgreSqlBuilder("pgvector/pgvector:pg16")
            .WithDatabase("advanced_rag_user_administration_repository_test")
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
        var actorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var targetUnitId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        long versionBefore;

        await using (var db = new AppDbContext(options))
        {
            await db.Database.MigrateAsync();

            // Seed a destination organizational unit (child of the seeded root) with its
            // closure rows, mirroring EfOrganizationalUnitRepository.CreateAsync.
            db.OrganizationalUnits.Add(new OrganizationalUnit
            {
                Id = targetUnitId,
                Name = "Engineering",
                ParentId = User.RootOrganizationalUnitId,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            db.OrganizationalUnitClosure.Add(new OrganizationalUnitClosure
            {
                AncestorId = targetUnitId,
                DescendantId = targetUnitId,
                Depth = 0,
            });
            db.OrganizationalUnitClosure.Add(new OrganizationalUnitClosure
            {
                AncestorId = User.RootOrganizationalUnitId,
                DescendantId = targetUnitId,
                Depth = 1,
            });

            // Seed a user on the default (root) unit.
            db.Users.Add(new User
            {
                Id = userId,
                Email = "viewer@example.com",
                DisplayName = "Viewer",
                PasswordHash = "hash",
                OrganizationalUnitId = User.RootOrganizationalUnitId,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();

            versionBefore = await db.Users
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => user.AccessScopeVersion)
                .SingleAsync();
        }

        await using (var db = new AppDbContext(options))
        {
            var repository = new EfUserAdministrationRepository(db);

            var updated = await repository.SetUserOrganizationalUnitAsync(
                userId,
                targetUnitId,
                actorId,
                CancellationToken.None);

            updated.Should().NotBeNull();
            updated!.OrganizationalUnit.Should().NotBeNull();
            updated.OrganizationalUnit!.Id.Should().Be(targetUnitId);
        }

        await using (var db = new AppDbContext(options))
        {
            var versionAfter = await db.Users
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => user.AccessScopeVersion)
                .SingleAsync();

            versionAfter.Should().Be(versionBefore + 1);
        }
    }
}
