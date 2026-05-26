using AdvancedRag.App.Configuration;
using AdvancedRag.App.Users;

namespace AdvancedRag.App.Setup;

public sealed record SetupStatus(
    bool SetupRequired,
    bool AdminExists,
    IReadOnlyList<string> RequiredRoles);

public sealed record CreateFirstAdminCommand(
    string Email,
    string DisplayName,
    string Password);

public sealed record FirstAdminResult(UserManagementUser User);

public interface ISetupRepository
{
    Task<bool> AdminExistsAsync(CancellationToken ct);

    Task<UserManagementUser?> CreateFirstAdminAsync(
        UserDraft user,
        UserBudgetDraft budget,
        TenantConfigDraft tenantConfig,
        IReadOnlyList<string> requiredRoles,
        CancellationToken ct);
}

public sealed class SetupException : Exception
{
    public SetupException(
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
