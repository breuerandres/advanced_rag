using System.Data;
using AdvancedRag.App.Auth;
using AdvancedRag.App.Setup;
using AdvancedRag.App.Users;
using AdvancedRag.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdvancedRag.Infrastructure.Setup;

public sealed class EfSetupRepository : ISetupRepository
{
    private const long SetupAdvisoryLockKey = 7_450_017_500_001;
    private readonly AppDbContext _db;

    public EfSetupRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<bool> AdminExistsAsync(CancellationToken ct)
    {
        return AdminExistsInternalAsync(ct);
    }

    public async Task<UserManagementUser?> CreateFirstAdminAsync(
        UserDraft user,
        UserBudgetDraft budget,
        IReadOnlyList<string> requiredRoles,
        CancellationToken ct)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await _db.Database.ExecuteSqlRawAsync($"select pg_advisory_xact_lock({SetupAdvisoryLockKey})", ct);

        if (await AdminExistsInternalAsync(ct))
        {
            await transaction.RollbackAsync(ct);
            return null;
        }

        IReadOnlyDictionary<string, Role> roles = await EnsureRolesAsync(requiredRoles, ct);
        Role adminRole = roles["Admin"];

        _db.Users.Add(new User
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            PasswordHash = user.PasswordHash,
            IsActive = user.IsActive,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = adminRole.Id });
        _db.UserAiBudgetLimits.Add(new UserAiBudgetLimit
        {
            UserId = user.Id,
            MonthlyBudgetUsd = budget.MonthlyBudgetUsd,
            IsDisabled = budget.IsDisabled,
            UpdatedAt = DateTimeOffset.UtcNow,
            UpdatedByUserId = budget.UpdatedByUserId,
        });

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new UserManagementUser(
            user.Id,
            user.Email,
            user.DisplayName,
            true,
            ["Admin"],
            [],
            AccessScopeHash.Compute("Admin", []),
            budget.MonthlyBudgetUsd,
            0m,
            budget.IsDisabled);
    }

    private async Task<bool> AdminExistsInternalAsync(CancellationToken ct)
    {
        return await (
                from userRole in _db.UserRoles.AsNoTracking()
                join role in _db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where role.Name == "Admin"
                select userRole.UserId)
            .AnyAsync(ct);
    }

    private async Task<IReadOnlyDictionary<string, Role>> EnsureRolesAsync(
        IReadOnlyList<string> requiredRoles,
        CancellationToken ct)
    {
        List<Role> existing = await _db.Roles
            .Where(role => requiredRoles.Contains(role.Name))
            .ToListAsync(ct);
        HashSet<string> existingNames = existing.Select(role => role.Name).ToHashSet(StringComparer.Ordinal);
        Role[] missing = requiredRoles
            .Where(roleName => !existingNames.Contains(roleName))
            .Select(roleName => new Role { Id = Guid.NewGuid(), Name = roleName })
            .ToArray();

        if (missing.Length > 0)
        {
            _db.Roles.AddRange(missing);
            existing.AddRange(missing);
        }

        return existing.ToDictionary(role => role.Name, StringComparer.Ordinal);
    }
}
