using AdvancedRag.App.Auth;
using AdvancedRag.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdvancedRag.Infrastructure.Auth;

public sealed class EfUserAccountRepository : IUserAccountRepository
{
    private readonly AppDbContext _db;

    public EfUserAccountRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<UserAccountRecord?> FindByIdAsync(Guid userId, CancellationToken ct)
    {
        User? user = await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == userId && item.IsActive, ct);

        return user is null
            ? null
            : new UserAccountRecord(
                user.Id,
                user.Email,
                user.DisplayName,
                user.PasswordHash,
                await ReadRolesAsync(user.Id, ct),
                await ReadGroupsAsync(user.Id, ct));
    }

    public async Task<bool> EmailExistsForAnotherUserAsync(Guid userId, string normalizedEmail, CancellationToken ct)
    {
        return await _db.Users.AnyAsync(
            user => user.Id != userId && user.Email.ToLower() == normalizedEmail,
            ct);
    }

    public async Task<UserAccountRecord> UpdateEmailAsync(Guid userId, string normalizedEmail, CancellationToken ct)
    {
        User user = await _db.Users.SingleAsync(item => item.Id == userId, ct);
        user.Email = normalizedEmail;
        await _db.SaveChangesAsync(ct);
        return (await FindByIdAsync(userId, ct))!;
    }

    public async Task UpdatePasswordHashAsync(Guid userId, string passwordHash, CancellationToken ct)
    {
        User user = await _db.Users.SingleAsync(item => item.Id == userId, ct);
        user.PasswordHash = passwordHash;
        await _db.SaveChangesAsync(ct);
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
                orderby groupEntity.Name
                select new AuthGroup(groupEntity.Id, groupEntity.Name))
            .ToListAsync(ct);
    }
}
