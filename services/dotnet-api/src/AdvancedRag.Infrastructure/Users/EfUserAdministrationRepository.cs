using AdvancedRag.App.Auth;
using AdvancedRag.App.Users;
using AdvancedRag.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdvancedRag.Infrastructure.Users;

public sealed class EfUserAdministrationRepository : IUserAdministrationRepository
{
    private readonly AppDbContext _db;

    public EfUserAdministrationRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<UserManagementUser>> ListUsersAsync(CancellationToken ct)
    {
        var users = await _db.Users
            .AsNoTracking()
            .OrderBy(user => user.DisplayName)
            .ThenBy(user => user.Email)
            .ToListAsync(ct);

        return await BuildUsersAsync(users, ct);
    }

    public async Task<IReadOnlyList<GroupRecord>> ListGroupsAsync(CancellationToken ct)
    {
        return await _db.Groups
            .AsNoTracking()
            .OrderBy(group => group.Name)
            .Select(group => new GroupRecord(group.Id, group.Name))
            .ToListAsync(ct);
    }

    public async Task<GroupRecord> CreateGroupAsync(string name, Guid actorUserId, CancellationToken ct)
    {
        var group = new Group
        {
            Id = Guid.NewGuid(),
            Name = name,
        };

        _db.Groups.Add(group);
        await _db.SaveChangesAsync(ct);
        return new GroupRecord(group.Id, group.Name);
    }

    public async Task<GroupRecord?> UpdateGroupAsync(Guid groupId, string name, Guid actorUserId, CancellationToken ct)
    {
        var group = await _db.Groups.SingleOrDefaultAsync(item => item.Id == groupId, ct);
        if (group is null)
        {
            return null;
        }

        group.Name = name;
        await _db.SaveChangesAsync(ct);
        return new GroupRecord(group.Id, group.Name);
    }

    public async Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct)
    {
        return await _db.Users.AnyAsync(user => user.Email.ToLower() == normalizedEmail, ct);
    }

    public async Task<IReadOnlyList<RoleRecord>> FindRolesAsync(
        IReadOnlyList<string> roleNames,
        CancellationToken ct)
    {
        return await _db.Roles
            .AsNoTracking()
            .Where(role => roleNames.Contains(role.Name))
            .OrderBy(role => role.Name)
            .Select(role => new RoleRecord(role.Name))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<GroupRecord>> FindGroupsAsync(
        IReadOnlyList<Guid> groupIds,
        CancellationToken ct)
    {
        return await _db.Groups
            .AsNoTracking()
            .Where(group => groupIds.Contains(group.Id))
            .OrderBy(group => group.Name)
            .Select(group => new GroupRecord(group.Id, group.Name))
            .ToListAsync(ct);
    }

    public async Task<UserManagementUser> CreateUserAsync(
        UserDraft user,
        IReadOnlyList<string> roleNames,
        IReadOnlyList<Guid> groupIds,
        UserBudgetDraft budget,
        CancellationToken ct)
    {
        var userEntity = new User
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            PasswordHash = user.PasswordHash,
            IsActive = user.IsActive,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _db.Users.Add(userEntity);
        await AddRoleLinksAsync(user.Id, roleNames, ct);
        AddGroupLinks(user.Id, groupIds);
        _db.UserAiBudgetLimits.Add(new UserAiBudgetLimit
        {
            UserId = user.Id,
            MonthlyBudgetUsd = budget.MonthlyBudgetUsd,
            IsDisabled = budget.IsDisabled,
            UpdatedAt = DateTimeOffset.UtcNow,
            UpdatedByUserId = budget.UpdatedByUserId,
        });

        await _db.SaveChangesAsync(ct);
        return (await FindUserAsync(user.Id, ct))!;
    }

    public async Task<UserManagementUser?> FindUserAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == userId, ct);
        if (user is null)
        {
            return null;
        }

        return (await BuildUsersAsync([user], ct)).Single();
    }

    public async Task<UserManagementUser?> SetUserRolesAsync(
        Guid userId,
        IReadOnlyList<string> roleNames,
        Guid actorUserId,
        CancellationToken ct)
    {
        if (!await _db.Users.AnyAsync(user => user.Id == userId, ct))
        {
            return null;
        }

        await _db.UserRoles.Where(userRole => userRole.UserId == userId).ExecuteDeleteAsync(ct);
        await AddRoleLinksAsync(userId, roleNames, ct);
        await _db.SaveChangesAsync(ct);
        return await FindUserAsync(userId, ct);
    }

    public async Task<UserManagementUser?> SetUserGroupsAsync(
        Guid userId,
        IReadOnlyList<Guid> groupIds,
        Guid actorUserId,
        CancellationToken ct)
    {
        if (!await _db.Users.AnyAsync(user => user.Id == userId, ct))
        {
            return null;
        }

        await _db.UserGroups.Where(userGroup => userGroup.UserId == userId).ExecuteDeleteAsync(ct);
        AddGroupLinks(userId, groupIds);
        await _db.SaveChangesAsync(ct);
        return await FindUserAsync(userId, ct);
    }

    public async Task<UserManagementUser?> SetUserActiveStatusAsync(
        Guid userId,
        bool isActive,
        Guid actorUserId,
        CancellationToken ct)
    {
        var user = await _db.Users.SingleOrDefaultAsync(item => item.Id == userId, ct);
        if (user is null)
        {
            return null;
        }

        user.IsActive = isActive;
        await _db.SaveChangesAsync(ct);
        return await FindUserAsync(userId, ct);
    }

    public async Task<UserManagementUser?> SetUserAiBudgetAsync(
        Guid userId,
        decimal? monthlyBudgetUsd,
        bool isDisabled,
        Guid actorUserId,
        CancellationToken ct)
    {
        if (!await _db.Users.AnyAsync(user => user.Id == userId, ct))
        {
            return null;
        }

        var budget = await _db.UserAiBudgetLimits.SingleOrDefaultAsync(item => item.UserId == userId, ct);
        if (budget is null)
        {
            _db.UserAiBudgetLimits.Add(new UserAiBudgetLimit
            {
                UserId = userId,
                MonthlyBudgetUsd = monthlyBudgetUsd,
                IsDisabled = isDisabled,
                UpdatedAt = DateTimeOffset.UtcNow,
                UpdatedByUserId = actorUserId,
            });
        }
        else
        {
            budget.MonthlyBudgetUsd = monthlyBudgetUsd;
            budget.IsDisabled = isDisabled;
            budget.UpdatedAt = DateTimeOffset.UtcNow;
            budget.UpdatedByUserId = actorUserId;
        }

        await _db.SaveChangesAsync(ct);
        return await FindUserAsync(userId, ct);
    }

    private async Task<IReadOnlyList<UserManagementUser>> BuildUsersAsync(
        IReadOnlyList<User> users,
        CancellationToken ct)
    {
        if (users.Count == 0)
        {
            return [];
        }

        var userIds = users.Select(user => user.Id).ToArray();
        var roleRows = await (
                from userRole in _db.UserRoles.AsNoTracking()
                join role in _db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where userIds.Contains(userRole.UserId)
                orderby role.Name
                select new { userRole.UserId, role.Name })
            .ToListAsync(ct);
        var groupRows = await (
                from userGroup in _db.UserGroups.AsNoTracking()
                join groupEntity in _db.Groups.AsNoTracking() on userGroup.GroupId equals groupEntity.Id
                where userIds.Contains(userGroup.UserId)
                orderby groupEntity.Name
                select new { userGroup.UserId, Group = new GroupRecord(groupEntity.Id, groupEntity.Name) })
            .ToListAsync(ct);
        var budgets = await _db.UserAiBudgetLimits
            .AsNoTracking()
            .Where(budget => userIds.Contains(budget.UserId))
            .ToDictionaryAsync(budget => budget.UserId, ct);

        return users
            .Select(user =>
            {
                var roles = roleRows
                    .Where(row => row.UserId == user.Id)
                    .Select(row => row.Name)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                var groups = groupRows
                    .Where(row => row.UserId == user.Id)
                    .Select(row => row.Group)
                    .Distinct()
                    .OrderBy(group => group.Name, StringComparer.Ordinal)
                    .ToArray();
                var budget = budgets.GetValueOrDefault(user.Id);
                decimal? monthlyBudget = budget?.IsDisabled == true
                    ? null
                    : budget?.MonthlyBudgetUsd ?? UserAdministrationService.DefaultMonthlyBudgetUsd;

                return new UserManagementUser(
                    user.Id,
                    user.Email,
                    user.DisplayName,
                    user.IsActive,
                    roles,
                    groups,
                    AccessScopeHash.Compute(PrimaryRole(roles), groups.Select(group => group.Id)),
                    monthlyBudget,
                    0m,
                    budget?.IsDisabled ?? false);
            })
            .ToArray();
    }

    private async Task AddRoleLinksAsync(Guid userId, IReadOnlyList<string> roleNames, CancellationToken ct)
    {
        var roleIds = await _db.Roles
            .Where(role => roleNames.Contains(role.Name))
            .Select(role => role.Id)
            .ToListAsync(ct);

        _db.UserRoles.AddRange(roleIds.Select(roleId => new UserRole { UserId = userId, RoleId = roleId }));
    }

    private void AddGroupLinks(Guid userId, IReadOnlyList<Guid> groupIds)
    {
        _db.UserGroups.AddRange(groupIds.Select(groupId => new UserGroup { UserId = userId, GroupId = groupId }));
    }

    private static string PrimaryRole(IReadOnlyList<string> roles)
    {
        if (roles.Contains("Admin", StringComparer.Ordinal))
        {
            return "Admin";
        }

        if (roles.Contains("DocumentManager", StringComparer.Ordinal))
        {
            return "DocumentManager";
        }

        return roles.Contains("Viewer", StringComparer.Ordinal) ? "Viewer" : roles.FirstOrDefault() ?? "Viewer";
    }
}
