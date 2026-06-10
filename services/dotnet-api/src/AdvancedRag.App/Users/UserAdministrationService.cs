using AdvancedRag.App.Auth;

namespace AdvancedRag.App.Users;

public interface IUserAdministrationService
{
    Task<IReadOnlyList<UserManagementUser>> ListUsersAsync(CancellationToken ct);

    Task<IReadOnlyList<GroupRecord>> ListGroupsAsync(CancellationToken ct);

    Task<GroupRecord> CreateGroupAsync(CreateGroupCommand command, CancellationToken ct);

    Task<GroupRecord> UpdateGroupAsync(UpdateGroupCommand command, CancellationToken ct);

    Task<UserManagementUser> CreateUserAsync(CreateUserCommand command, CancellationToken ct);

    Task<UserManagementUser?> GetUserAsync(Guid userId, CancellationToken ct);

    Task<UserManagementUser> SetUserRolesAsync(SetUserRolesCommand command, CancellationToken ct);

    Task<UserManagementUser> SetUserGroupsAsync(SetUserGroupsCommand command, CancellationToken ct);

    Task<UserManagementUser> SetUserActiveStatusAsync(SetUserActiveStatusCommand command, CancellationToken ct);

    Task<UserManagementUser> SetUserAiBudgetAsync(SetUserAiBudgetCommand command, CancellationToken ct);
}

public sealed class UserAdministrationService : IUserAdministrationService
{
    public const decimal DefaultMonthlyBudgetUsd = 5m;

    private readonly IUserAdministrationRepository _repository;
    private readonly IPasswordHashService _passwords;

    public UserAdministrationService(
        IUserAdministrationRepository repository,
        IPasswordHashService passwords)
    {
        _repository = repository;
        _passwords = passwords;
    }

    public Task<IReadOnlyList<UserManagementUser>> ListUsersAsync(CancellationToken ct)
    {
        return _repository.ListUsersAsync(ct);
    }

    public Task<IReadOnlyList<GroupRecord>> ListGroupsAsync(CancellationToken ct)
    {
        return _repository.ListGroupsAsync(ct);
    }

    public async Task<GroupRecord> CreateGroupAsync(CreateGroupCommand command, CancellationToken ct)
    {
        var name = NormalizeRequired(command.Name, "name");
        string publishingPolicy = NormalizePublishingPolicy(command.PublishingPolicy);
        return await _repository.CreateGroupAsync(
            name,
            command.OwnerOrganizationalUnitId,
            publishingPolicy,
            command.ActorUserId,
            ct);
    }

    public async Task<GroupRecord> UpdateGroupAsync(UpdateGroupCommand command, CancellationToken ct)
    {
        var name = NormalizeRequired(command.Name, "name");
        string publishingPolicy = NormalizePublishingPolicy(command.PublishingPolicy);
        var updated = await _repository.UpdateGroupAsync(
            command.GroupId,
            name,
            command.OwnerOrganizationalUnitId,
            publishingPolicy,
            command.ActorUserId,
            ct);
        return updated
            ?? throw new UserAdministrationException("NOT_FOUND", 404, "Group not found.");
    }

    public async Task<UserManagementUser> CreateUserAsync(CreateUserCommand command, CancellationToken ct)
    {
        var email = NormalizeRequired(command.Email, "email");
        var displayName = NormalizeRequired(command.DisplayName, "displayName");
        var password = NormalizeRequired(command.Password, "password");
        var roleNames = NormalizeRoleNames(command.RoleNames);
        var groupIds = NormalizeGroupIds(command.GroupIds);
        Guid organizationalUnitId = command.OrganizationalUnitId
            ?? throw new UserAdministrationException(
                "VALIDATION_FAILED",
                400,
                "Organizational unit is required.",
                new Dictionary<string, object?> { ["field"] = "organizationalUnitId" });

        await RequireKnownRolesAsync(roleNames, ct);
        await RequireKnownGroupsAsync(groupIds, ct);
        await RequireKnownOrganizationalUnitAsync(organizationalUnitId, ct);

        if (await _repository.EmailExistsAsync(email, ct))
        {
            throw new UserAdministrationException(
                "CONFLICT",
                409,
                "A user with this email already exists.",
                new Dictionary<string, object?> { ["field"] = "email" });
        }

        var user = new UserDraft(
            Guid.NewGuid(),
            email,
            displayName,
            _passwords.Hash(password),
            true,
            roleNames,
            groupIds,
            organizationalUnitId);
        var budget = new UserBudgetDraft(
            user.Id,
            DefaultMonthlyBudgetUsd,
            false,
            command.ActorUserId);

        return await _repository.CreateUserAsync(user, roleNames, groupIds, budget, ct);
    }

    public Task<UserManagementUser?> GetUserAsync(Guid userId, CancellationToken ct)
    {
        return _repository.FindUserAsync(userId, ct);
    }

    public async Task<UserManagementUser> SetUserRolesAsync(SetUserRolesCommand command, CancellationToken ct)
    {
        var roleNames = NormalizeRoleNames(command.RoleNames);
        await RequireKnownRolesAsync(roleNames, ct);

        var updated = await _repository.SetUserRolesAsync(command.UserId, roleNames, command.ActorUserId, ct);
        return RequireFound(updated);
    }

    public async Task<UserManagementUser> SetUserGroupsAsync(SetUserGroupsCommand command, CancellationToken ct)
    {
        var groupIds = NormalizeGroupIds(command.GroupIds);
        await RequireKnownGroupsAsync(groupIds, ct);

        var updated = await _repository.SetUserGroupsAsync(command.UserId, groupIds, command.ActorUserId, ct);
        return RequireFound(updated);
    }

    public async Task<UserManagementUser> SetUserActiveStatusAsync(
        SetUserActiveStatusCommand command,
        CancellationToken ct)
    {
        var updated = await _repository.SetUserActiveStatusAsync(
            command.UserId,
            command.IsActive,
            command.ActorUserId,
            ct);

        return RequireFound(updated);
    }

    public async Task<UserManagementUser> SetUserAiBudgetAsync(
        SetUserAiBudgetCommand command,
        CancellationToken ct)
    {
        if (command.MonthlyBudgetUsd < 0)
        {
            throw new UserAdministrationException(
                "VALIDATION_FAILED",
                400,
                "Monthly budget must be non-negative.",
                new Dictionary<string, object?> { ["field"] = "monthlyBudgetUsd" });
        }

        var updated = await _repository.SetUserAiBudgetAsync(
            command.UserId,
            command.IsDisabled ? null : command.MonthlyBudgetUsd,
            command.IsDisabled,
            command.ActorUserId,
            ct);

        return RequireFound(updated);
    }

    private async Task RequireKnownRolesAsync(IReadOnlyList<string> roleNames, CancellationToken ct)
    {
        var roles = await _repository.FindRolesAsync(roleNames, ct);
        var found = roles.Select(role => role.Name).ToHashSet(StringComparer.Ordinal);
        var missing = roleNames.Where(role => !found.Contains(role)).ToArray();
        if (missing.Length > 0)
        {
            throw new UserAdministrationException(
                "VALIDATION_FAILED",
                400,
                "One or more roles are invalid.",
                new Dictionary<string, object?> { ["roles"] = missing });
        }
    }

    private async Task RequireKnownGroupsAsync(IReadOnlyList<Guid> groupIds, CancellationToken ct)
    {
        if (groupIds.Count == 0)
        {
            return;
        }

        var groups = await _repository.FindGroupsAsync(groupIds, ct);
        var found = groups.Select(group => group.Id).ToHashSet();
        var missing = groupIds.Where(groupId => !found.Contains(groupId)).ToArray();
        if (missing.Length > 0)
        {
            throw new UserAdministrationException(
                "VALIDATION_FAILED",
                400,
                "One or more groups are invalid.",
                new Dictionary<string, object?> { ["groups"] = missing });
        }
    }

    private async Task RequireKnownOrganizationalUnitAsync(Guid organizationalUnitId, CancellationToken ct)
    {
        OrganizationalUnitRecord? unit = await _repository.FindOrganizationalUnitAsync(organizationalUnitId, ct);
        if (unit is null || !unit.IsActive)
        {
            throw new UserAdministrationException(
                "VALIDATION_FAILED",
                400,
                "Organizational unit is invalid.",
                new Dictionary<string, object?> { ["field"] = "organizationalUnitId" });
        }
    }

    private static UserManagementUser RequireFound(UserManagementUser? user)
    {
        return user
            ?? throw new UserAdministrationException("NOT_FOUND", 404, "User not found.");
    }

    private static string NormalizeRequired(string value, string field)
    {
        var normalized = value.Trim();
        if (normalized.Length == 0)
        {
            throw new UserAdministrationException(
                "VALIDATION_FAILED",
                400,
                $"{field} is required.",
                new Dictionary<string, object?> { ["field"] = field });
        }

        return field == "email" ? normalized.ToLowerInvariant() : normalized;
    }

    private static IReadOnlyList<string> NormalizeRoleNames(IReadOnlyList<string> roleNames)
    {
        var normalized = roleNames
            .Select(role => role.Trim())
            .Where(role => role.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (normalized.Length == 0)
        {
            throw new UserAdministrationException(
                "VALIDATION_FAILED",
                400,
                "At least one role is required.",
                new Dictionary<string, object?> { ["field"] = "roles" });
        }

        return normalized;
    }

    private static IReadOnlyList<Guid> NormalizeGroupIds(IReadOnlyList<Guid> groupIds)
    {
        return groupIds
            .Distinct()
            .Order()
            .ToArray();
    }

    private static string NormalizePublishingPolicy(string publishingPolicy)
    {
        string normalized = publishingPolicy.Trim();
        string[] allowed = ["OwnerScope", "ExplicitGrantOnly", "AdminOnly"];
        if (!allowed.Contains(normalized, StringComparer.Ordinal))
        {
            throw new UserAdministrationException(
                "VALIDATION_FAILED",
                400,
                "Publishing policy is invalid.",
                new Dictionary<string, object?> { ["field"] = "publishingPolicy" });
        }

        return normalized;
    }
}

public sealed class OrganizationalUnitService : IOrganizationalUnitService
{
    private readonly IOrganizationalUnitRepository _repository;

    public OrganizationalUnitService(IOrganizationalUnitRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<OrganizationalUnitRecord>> ListTreeAsync(bool includeInactive, CancellationToken ct)
    {
        return _repository.ListTreeAsync(includeInactive, ct);
    }

    public async Task<OrganizationalUnitRecord> CreateAsync(
        CreateOrganizationalUnitCommand command,
        CancellationToken ct)
    {
        string name = NormalizeRequired(command.Name, "name");
        OrganizationalUnitRecord? parent = await _repository.FindAsync(command.ParentId, ct);
        if (parent is null || !parent.IsActive)
        {
            throw new UserAdministrationException(
                "VALIDATION_FAILED",
                400,
                "Parent organizational unit is invalid.",
                new Dictionary<string, object?> { ["field"] = "parentId" });
        }

        return await _repository.CreateAsync(name, command.ParentId, command.ActorUserId, ct);
    }

    public async Task<OrganizationalUnitRecord> UpdateAsync(
        UpdateOrganizationalUnitCommand command,
        CancellationToken ct)
    {
        string? name = command.Name is null ? null : NormalizeRequired(command.Name, "name");
        OrganizationalUnitRecord? updated = await _repository.UpdateAsync(
            command.Id,
            name,
            command.IsActive,
            command.ActorUserId,
            ct);

        return updated
            ?? throw new UserAdministrationException("NOT_FOUND", 404, "Organizational unit not found.");
    }

    private static string NormalizeRequired(string value, string field)
    {
        string normalized = value.Trim();
        if (normalized.Length == 0)
        {
            throw new UserAdministrationException(
                "VALIDATION_FAILED",
                400,
                $"{field} is required.",
                new Dictionary<string, object?> { ["field"] = field });
        }

        return normalized;
    }
}
