namespace AdvancedRag.App.Auth;

public sealed record AuthenticatedUser(
    Guid Id,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<AuthGroup> Groups)
{
    public string PrimaryRole
    {
        get
        {
            if (Roles.Contains("Admin", StringComparer.Ordinal))
            {
                return "Admin";
            }

            if (Roles.Contains("DocumentPublisher", StringComparer.Ordinal))
            {
                return "DocumentPublisher";
            }

            if (Roles.Contains("DocumentEditor", StringComparer.Ordinal))
            {
                return "DocumentEditor";
            }

            return "Viewer";
        }
    }
}

public sealed record AuthenticatedUserWithPassword(
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

public sealed record AuthGroup(Guid Id, string Name);

public interface IUserAuthRepository
{
    Task<AuthenticatedUserWithPassword?> FindActiveByEmailAsync(string normalizedEmail, CancellationToken ct);

    Task<AuthenticatedUser?> FindActiveByIdAsync(Guid userId, CancellationToken ct);
}

public interface IPasswordHashService
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}
