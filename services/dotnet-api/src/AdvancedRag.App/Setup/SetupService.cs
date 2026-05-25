using AdvancedRag.App.Auth;
using AdvancedRag.App.Users;

namespace AdvancedRag.App.Setup;

public interface ISetupService
{
    Task<SetupStatus> GetStatusAsync(CancellationToken ct);

    Task<FirstAdminResult> CreateFirstAdminAsync(CreateFirstAdminCommand command, CancellationToken ct);
}

public sealed class SetupService : ISetupService
{
    public static readonly IReadOnlyList<string> RequiredRoleNames =
    [
        "Admin",
        "DocumentManager",
        "Viewer",
    ];

    private readonly ISetupRepository _repository;
    private readonly IPasswordHashService _passwords;

    public SetupService(ISetupRepository repository, IPasswordHashService passwords)
    {
        _repository = repository;
        _passwords = passwords;
    }

    public async Task<SetupStatus> GetStatusAsync(CancellationToken ct)
    {
        bool adminExists = await _repository.AdminExistsAsync(ct);
        return new SetupStatus(!adminExists, adminExists, RequiredRoleNames);
    }

    public async Task<FirstAdminResult> CreateFirstAdminAsync(
        CreateFirstAdminCommand command,
        CancellationToken ct)
    {
        if (await _repository.AdminExistsAsync(ct))
        {
            throw new SetupException(
                "SETUP_ALREADY_COMPLETED",
                409,
                "First-run setup is already completed.");
        }

        string email = NormalizeEmail(command.Email);
        string displayName = NormalizeRequired(command.DisplayName, "displayName");
        string password = NormalizeRequired(command.Password, "password");

        var user = new UserDraft(
            Guid.NewGuid(),
            email,
            displayName,
            _passwords.Hash(password),
            true,
            ["Admin"],
            []);
        var budget = new UserBudgetDraft(
            user.Id,
            UserAdministrationService.DefaultMonthlyBudgetUsd,
            false,
            user.Id);

        UserManagementUser? created = await _repository.CreateFirstAdminAsync(
            user,
            budget,
            RequiredRoleNames,
            ct);

        return created is null
            ? throw new SetupException(
                "SETUP_ALREADY_COMPLETED",
                409,
                "First-run setup is already completed.")
            : new FirstAdminResult(created);
    }

    private static string NormalizeEmail(string value)
    {
        string email = NormalizeRequired(value, "email").ToLowerInvariant();
        if (!email.Contains('@', StringComparison.Ordinal))
        {
            throw new SetupException(
                "VALIDATION_FAILED",
                400,
                "Email format is invalid.",
                new Dictionary<string, object?> { ["field"] = "email" });
        }

        return email;
    }

    private static string NormalizeRequired(string value, string field)
    {
        string normalized = value.Trim();
        if (normalized.Length == 0)
        {
            throw new SetupException(
                "VALIDATION_FAILED",
                400,
                $"{field} is required.",
                new Dictionary<string, object?> { ["field"] = field });
        }

        return normalized;
    }
}
