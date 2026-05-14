namespace AdvancedRag.App.Users;

public sealed record CreateUserCommand(
    string Email,
    string DisplayName,
    string Password,
    IReadOnlyList<string> RoleNames,
    IReadOnlyList<Guid> GroupIds,
    Guid ActorUserId);

public sealed record SetUserRolesCommand(
    Guid UserId,
    IReadOnlyList<string> RoleNames,
    Guid ActorUserId);

public sealed record SetUserGroupsCommand(
    Guid UserId,
    IReadOnlyList<Guid> GroupIds,
    Guid ActorUserId);

public sealed record SetUserActiveStatusCommand(
    Guid UserId,
    bool IsActive,
    Guid ActorUserId);

public sealed record SetUserAiBudgetCommand(
    Guid UserId,
    decimal? MonthlyBudgetUsd,
    bool IsDisabled,
    Guid ActorUserId);

public sealed record CreateGroupCommand(string Name, Guid ActorUserId);

public sealed record UserManagementUser(
    Guid Id,
    string Email,
    string DisplayName,
    bool IsActive,
    IReadOnlyList<string> Roles,
    IReadOnlyList<GroupRecord> Groups,
    string AccessScopeHash,
    decimal? MonthlyBudgetUsd,
    decimal CurrentSpendUsd,
    bool IsBudgetDisabled)
{
    public decimal? RemainingBudgetUsd
        => IsBudgetDisabled || MonthlyBudgetUsd is null
            ? null
            : Math.Max(0m, MonthlyBudgetUsd.Value - CurrentSpendUsd);
}

public sealed record UserDraft(
    Guid Id,
    string Email,
    string DisplayName,
    string PasswordHash,
    bool IsActive,
    IReadOnlyList<string> RoleNames,
    IReadOnlyList<Guid> GroupIds);

public sealed record UserBudgetDraft(
    Guid UserId,
    decimal? MonthlyBudgetUsd,
    bool IsDisabled,
    Guid UpdatedByUserId);

public sealed record RoleRecord(string Name);

public sealed record GroupRecord(Guid Id, string Name);

public interface IUserAdministrationRepository
{
    Task<IReadOnlyList<UserManagementUser>> ListUsersAsync(CancellationToken ct);

    Task<IReadOnlyList<GroupRecord>> ListGroupsAsync(CancellationToken ct);

    Task<GroupRecord> CreateGroupAsync(string name, Guid actorUserId, CancellationToken ct);

    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct);

    Task<IReadOnlyList<RoleRecord>> FindRolesAsync(IReadOnlyList<string> roleNames, CancellationToken ct);

    Task<IReadOnlyList<GroupRecord>> FindGroupsAsync(IReadOnlyList<Guid> groupIds, CancellationToken ct);

    Task<UserManagementUser> CreateUserAsync(
        UserDraft user,
        IReadOnlyList<string> roleNames,
        IReadOnlyList<Guid> groupIds,
        UserBudgetDraft budget,
        CancellationToken ct);

    Task<UserManagementUser?> FindUserAsync(Guid userId, CancellationToken ct);

    Task<UserManagementUser?> SetUserRolesAsync(
        Guid userId,
        IReadOnlyList<string> roleNames,
        Guid actorUserId,
        CancellationToken ct);

    Task<UserManagementUser?> SetUserGroupsAsync(
        Guid userId,
        IReadOnlyList<Guid> groupIds,
        Guid actorUserId,
        CancellationToken ct);

    Task<UserManagementUser?> SetUserActiveStatusAsync(
        Guid userId,
        bool isActive,
        Guid actorUserId,
        CancellationToken ct);

    Task<UserManagementUser?> SetUserAiBudgetAsync(
        Guid userId,
        decimal? monthlyBudgetUsd,
        bool isDisabled,
        Guid actorUserId,
        CancellationToken ct);
}

public sealed class UserAdministrationException : Exception
{
    public UserAdministrationException(
        string code,
        int httpStatus,
        string message,
        IReadOnlyDictionary<string, object?>? details = null)
        : base(message)
    {
        Code = code;
        HttpStatus = httpStatus;
        Details = details ?? new Dictionary<string, object?>();
    }

    public string Code { get; }

    public int HttpStatus { get; }

    public IReadOnlyDictionary<string, object?> Details { get; }
}
