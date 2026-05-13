namespace AdvancedRag.App.Auth;

public interface IAuthService
{
    Task<AuthenticatedUser?> AuthenticateAsync(string email, string password, CancellationToken ct);

    Task<AuthenticatedUser?> GetActiveUserAsync(Guid userId, CancellationToken ct);
}

public sealed class AuthService : IAuthService
{
    private readonly IUserAuthRepository _users;
    private readonly IPasswordHashService _passwords;

    public AuthService(IUserAuthRepository users, IPasswordHashService passwords)
    {
        _users = users;
        _passwords = passwords;
    }

    public async Task<AuthenticatedUser?> AuthenticateAsync(string email, string password, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var user = await _users.FindActiveByEmailAsync(email.Trim().ToLowerInvariant(), ct);
        if (user is null || !_passwords.Verify(password, user.PasswordHash))
        {
            return null;
        }

        return user.ToAuthenticatedUser();
    }

    public Task<AuthenticatedUser?> GetActiveUserAsync(Guid userId, CancellationToken ct)
    {
        return _users.FindActiveByIdAsync(userId, ct);
    }
}
