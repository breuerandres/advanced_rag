using AdvancedRag.App.Auth;
using AdvancedRag.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdvancedRag.Infrastructure.Auth;

public sealed class EfEffectiveAccessScopeRepository : IEffectiveAccessScopeRepository
{
    private readonly AppDbContext _db;

    public EfEffectiveAccessScopeRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<EffectiveAccessScope?> FindForActiveUserAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.IsActive && item.Id == userId, ct);

        if (user is null)
        {
            return null;
        }

        string[] roles = await (
                from userRole in _db.UserRoles.AsNoTracking()
                join role in _db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where userRole.UserId == userId
                orderby role.Name
                select role.Name)
            .ToArrayAsync(ct);

        Guid[] groupIds = await _db.UserGroups
            .AsNoTracking()
            .Where(userGroup => userGroup.UserId == userId)
            .Select(userGroup => userGroup.GroupId)
            .OrderBy(groupId => groupId)
            .ToArrayAsync(ct);

        string primaryRole = PrimaryRole(roles);
        return new EffectiveAccessScope(
            user.Id,
            primaryRole,
            primaryRole.Equals("Admin", StringComparison.Ordinal),
            user.OrganizationalUnitId,
            groupIds,
            user.AccessScopeVersion,
            "published");
    }

    private static string PrimaryRole(IReadOnlyList<string> roles)
    {
        if (roles.Contains("Admin", StringComparer.Ordinal))
        {
            return "Admin";
        }

        if (roles.Contains("DocumentPublisher", StringComparer.Ordinal))
        {
            return "DocumentPublisher";
        }

        if (roles.Contains("DocumentEditor", StringComparer.Ordinal))
        {
            return "DocumentEditor";
        }

        return "Viewer";
    }
}
