namespace AdvancedRag.App.Auth;

public interface IUserAccountService
{
    Task<AuthenticatedUser> UpdateEmailAsync(UpdateUserEmailCommand command, CancellationToken ct);

    Task ChangePasswordAsync(ChangeUserPasswordCommand command, CancellationToken ct);
}

public sealed class UserAccountService : IUserAccountService
{
    private readonly IUserAccountRepository _users;
    private readonly IPasswordHashService _passwords;

    public UserAccountService(IUserAccountRepository users, IPasswordHashService passwords)
    {
        _users = users;
        _passwords = passwords;
    }

    public async Task<AuthenticatedUser> UpdateEmailAsync(UpdateUserEmailCommand command, CancellationToken ct)
    {
        string normalizedEmail = NormalizeEmail(command.Email);
        if (!IsValidEmail(normalizedEmail))
        {
            throw ValidationFailed("email", "Enter a valid email address.");
        }

        UserAccountRecord user = await FindUserOrThrowAsync(command.UserId, ct);
        if (await _users.EmailExistsForAnotherUserAsync(user.Id, normalizedEmail, ct))
        {
            throw ValidationFailed("email", "Email is already in use.");
        }

        UserAccountRecord updated = await _users.UpdateEmailAsync(user.Id, normalizedEmail, ct);
        return updated.ToAuthenticatedUser();
    }

    public async Task ChangePasswordAsync(ChangeUserPasswordCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.CurrentPassword))
        {
            throw new UserAccountException("AUTH_REQUIRED", 401, "Current password is required.");
        }

        if (string.IsNullOrWhiteSpace(command.NewPassword))
        {
            throw ValidationFailed("newPassword", "Enter a new password.");
        }

        UserAccountRecord user = await FindUserOrThrowAsync(command.UserId, ct);
        if (!_passwords.Verify(command.CurrentPassword, user.PasswordHash))
        {
            throw new UserAccountException("AUTH_REQUIRED", 401, "Current password is invalid.");
        }

        await _users.UpdatePasswordHashAsync(user.Id, _passwords.Hash(command.NewPassword), ct);
    }

    private async Task<UserAccountRecord> FindUserOrThrowAsync(Guid userId, CancellationToken ct)
    {
        UserAccountRecord? user = await _users.FindByIdAsync(userId, ct);
        return user ?? throw new UserAccountException("AUTH_REQUIRED", 401, "Authentication required.");
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static bool IsValidEmail(string email)
    {
        int atIndex = email.IndexOf('@', StringComparison.Ordinal);
        return atIndex > 0 && atIndex < email.Length - 1;
    }

    private static UserAccountException ValidationFailed(string field, string message)
    {
        return new UserAccountException(
            "VALIDATION_FAILED",
            400,
            message,
            new Dictionary<string, object?> { ["field"] = field });
    }
}

public sealed record UpdateUserEmailCommand(Guid UserId, string Email);

public sealed record ChangeUserPasswordCommand(Guid UserId, string CurrentPassword, string NewPassword);

public sealed record UserAccountRecord(
    Guid Id,
    string Email,
    string DisplayName,
    string PasswordHash,
    IReadOnlyList<string> Roles,
    IReadOnlyList<AuthGroup> Groups)
{
    public AuthenticatedUser ToAuthenticatedUser()
    {
        return new AuthenticatedUser(Id, Email, DisplayName, Roles, Groups);
    }
}

public interface IUserAccountRepository
{
    Task<UserAccountRecord?> FindByIdAsync(Guid userId, CancellationToken ct);

    Task<bool> EmailExistsForAnotherUserAsync(Guid userId, string normalizedEmail, CancellationToken ct);

    Task<UserAccountRecord> UpdateEmailAsync(Guid userId, string normalizedEmail, CancellationToken ct);

    Task UpdatePasswordHashAsync(Guid userId, string passwordHash, CancellationToken ct);
}

public sealed class UserAccountException : Exception
{
    public UserAccountException(
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
