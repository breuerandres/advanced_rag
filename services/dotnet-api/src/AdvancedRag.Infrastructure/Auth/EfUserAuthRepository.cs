using AdvancedRag.App.Auth;
using AdvancedRag.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdvancedRag.Infrastructure.Auth;

public sealed class EfUserAuthRepository : IUserAuthRepository
{
    private readonly AppDbContext _db;

    public EfUserAuthRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AuthenticatedUserWithPassword?> FindActiveByEmailAsync(string normalizedEmail, CancellationToken ct)
    {
        var user = await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.IsActive && item.Email.ToLower() == normalizedEmail,
                ct);

        if (user is null)
        {
            return null;
        }

        var roles = await ReadRolesAsync(user.Id, ct);
        var groups = await ReadGroupsAsync(user.Id, ct);

        return new AuthenticatedUserWithPassword(
            user.Id,
            user.Email,
            user.DisplayName,
            user.PasswordHash,
            roles,
            groups);
    }

    public async Task<AuthenticatedUser?> FindActiveByIdAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.IsActive && item.Id == userId, ct);

        if (user is null)
        {
            return null;
        }

        return new AuthenticatedUser(
            user.Id,
            user.Email,
            user.DisplayName,
            await ReadRolesAsync(user.Id, ct),
            await ReadGroupsAsync(user.Id, ct));
    }

    private async Task<IReadOnlyList<string>> ReadRolesAsync(Guid userId, CancellationToken ct)
    {
        return await (
                from userRole in _db.UserRoles.AsNoTracking()
                join role in _db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where userRole.UserId == userId
                orderby role.Name
                select role.Name)
            .ToListAsync(ct);
    }

    private async Task<IReadOnlyList<AuthGroup>> ReadGroupsAsync(Guid userId, CancellationToken ct)
    {
        return await (
                from userGroup in _db.UserGroups.AsNoTracking()
                join groupEntity in _db.Groups.AsNoTracking() on userGroup.GroupId equals groupEntity.Id
                where userGroup.UserId == userId
                orderby groupEntity.Id
                select new AuthGroup(groupEntity.Id, groupEntity.Name))
            .ToListAsync(ct);
    }
}
