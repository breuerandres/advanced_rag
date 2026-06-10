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
        var groups = await _db.Groups
            .AsNoTracking()
            .OrderBy(group => group.Name)
            .ToListAsync(ct);

        return await BuildGroupsAsync(groups, ct);
    }

    public async Task<IReadOnlyList<OrganizationalUnitRecord>> ListActiveOrganizationalUnitsAsync(CancellationToken ct)
    {
        return await BuildOrganizationalUnitRecordsAsync(activeOnly: true, ct);
    }

    public async Task<GroupRecord> CreateGroupAsync(
        string name,
        Guid? ownerOrganizationalUnitId,
        string publishingPolicy,
        Guid actorUserId,
        CancellationToken ct)
    {
        var group = new Group
        {
            Id = Guid.NewGuid(),
            Name = name,
            OwnerOrganizationalUnitId = ownerOrganizationalUnitId,
            PublishingPolicy = publishingPolicy,
        };

        _db.Groups.Add(group);
        await _db.SaveChangesAsync(ct);
        return (await BuildGroupsAsync([group], ct)).Single();
    }

    public async Task<GroupRecord?> UpdateGroupAsync(
        Guid groupId,
        string name,
        Guid? ownerOrganizationalUnitId,
        string publishingPolicy,
        Guid actorUserId,
        CancellationToken ct)
    {
        var group = await _db.Groups.SingleOrDefaultAsync(item => item.Id == groupId, ct);
        if (group is null)
        {
            return null;
        }

        group.Name = name;
        group.OwnerOrganizationalUnitId = ownerOrganizationalUnitId;
        group.PublishingPolicy = publishingPolicy;
        await _db.SaveChangesAsync(ct);
        return (await BuildGroupsAsync([group], ct)).Single();
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
        var groups = await _db.Groups
            .AsNoTracking()
            .Where(group => groupIds.Contains(group.Id))
            .OrderBy(group => group.Name)
            .ToListAsync(ct);

        return await BuildGroupsAsync(groups, ct);
    }

    public async Task<OrganizationalUnitRecord?> FindOrganizationalUnitAsync(
        Guid organizationalUnitId,
        CancellationToken ct)
    {
        IReadOnlyList<OrganizationalUnitRecord> records = await BuildOrganizationalUnitRecordsAsync(activeOnly: false, ct);
        return records.SingleOrDefault(item => item.Id == organizationalUnitId);
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
            OrganizationalUnitId = user.OrganizationalUnitId,
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
        var user = await _db.Users.SingleOrDefaultAsync(item => item.Id == userId, ct);
        if (user is null)
        {
            return null;
        }

        await _db.UserRoles.Where(userRole => userRole.UserId == userId).ExecuteDeleteAsync(ct);
        await AddRoleLinksAsync(userId, roleNames, ct);
        IncrementAccessScopeVersion(user);
        await _db.SaveChangesAsync(ct);
        return await FindUserAsync(userId, ct);
    }

    public async Task<UserManagementUser?> SetUserGroupsAsync(
        Guid userId,
        IReadOnlyList<Guid> groupIds,
        Guid actorUserId,
        CancellationToken ct)
    {
        var user = await _db.Users.SingleOrDefaultAsync(item => item.Id == userId, ct);
        if (user is null)
        {
            return null;
        }

        await _db.UserGroups.Where(userGroup => userGroup.UserId == userId).ExecuteDeleteAsync(ct);
        AddGroupLinks(userId, groupIds);
        IncrementAccessScopeVersion(user);
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
        IncrementAccessScopeVersion(user);
        await _db.SaveChangesAsync(ct);
        return await FindUserAsync(userId, ct);
    }

    public async Task<UserManagementUser?> SetUserOrganizationalUnitAsync(
        Guid userId,
        Guid organizationalUnitId,
        Guid actorUserId,
        CancellationToken ct)
    {
        var user = await _db.Users.SingleOrDefaultAsync(item => item.Id == userId, ct);
        if (user is null)
        {
            return null;
        }

        user.OrganizationalUnitId = organizationalUnitId;
        IncrementAccessScopeVersion(user);
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
                select new { userGroup.UserId, Group = groupEntity })
            .ToListAsync(ct);
        IReadOnlyList<GroupRecord> allGroups = await BuildGroupsAsync(
            groupRows.Select(row => row.Group).DistinctBy(group => group.Id).ToArray(),
            ct);
        Dictionary<Guid, GroupRecord> groupsById = allGroups.ToDictionary(group => group.Id);
        IReadOnlyList<OrganizationalUnitRecord> organizationalUnits = await BuildOrganizationalUnitRecordsAsync(
            activeOnly: false,
            ct);
        Dictionary<Guid, OrganizationalUnitRecord> unitsById = organizationalUnits.ToDictionary(unit => unit.Id);
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
                    .Select(row => groupsById[row.Group.Id])
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
                    unitsById.GetValueOrDefault(user.OrganizationalUnitId),
                    AccessScopeHash.ComputeV2(
                        PrimaryRole(roles),
                        roles.Contains("Admin", StringComparer.Ordinal),
                        user.OrganizationalUnitId,
                        groups.Select(group => group.Id),
                        user.AccessScopeVersion),
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

    private static void IncrementAccessScopeVersion(User user)
    {
        checked
        {
            user.AccessScopeVersion += 1;
        }
    }

    private async Task<IReadOnlyList<GroupRecord>> BuildGroupsAsync(
        IReadOnlyList<Group> groups,
        CancellationToken ct)
    {
        if (groups.Count == 0)
        {
            return [];
        }

        IReadOnlyList<OrganizationalUnitRecord> units = await BuildOrganizationalUnitRecordsAsync(activeOnly: false, ct);
        Dictionary<Guid, OrganizationalUnitRecord> unitsById = units.ToDictionary(unit => unit.Id);

        return groups
            .Select(group => new GroupRecord(
                group.Id,
                group.Name,
                group.OwnerOrganizationalUnitId is { } ownerId ? unitsById.GetValueOrDefault(ownerId) : null,
                group.PublishingPolicy))
            .ToArray();
    }

    private async Task<IReadOnlyList<OrganizationalUnitRecord>> BuildOrganizationalUnitRecordsAsync(
        bool activeOnly,
        CancellationToken ct)
    {
        var units = await _db.OrganizationalUnits
            .AsNoTracking()
            .Where(unit => !activeOnly || unit.IsActive)
            .OrderBy(unit => unit.Name)
            .ToListAsync(ct);
        Guid[] unitIds = units.Select(unit => unit.Id).ToArray();
        var depths = await _db.OrganizationalUnitClosure
            .AsNoTracking()
            .Where(closure => unitIds.Contains(closure.DescendantId))
            .GroupBy(closure => closure.DescendantId)
            .Select(group => new { Id = group.Key, Depth = group.Max(closure => closure.Depth) })
            .ToDictionaryAsync(item => item.Id, item => item.Depth, ct);

        return units
            .Select(unit => new OrganizationalUnitRecord(
                unit.Id,
                unit.Name,
                unit.ParentId,
                depths.GetValueOrDefault(unit.Id),
                unit.IsActive))
            .OrderBy(unit => unit.Depth)
            .ThenBy(unit => unit.Name, StringComparer.Ordinal)
            .ToArray();
    }
}
