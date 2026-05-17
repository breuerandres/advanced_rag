using AdvancedRag.App.Auth;

namespace AdvancedRag.Api.Models.Auth;

public sealed record SessionResponse(SessionUser User)
{
    public static SessionResponse FromUser(AuthenticatedUser user)
    {
        return new SessionResponse(
            new SessionUser(
                user.Id,
                user.Email,
                user.DisplayName,
                user.Roles,
                user.Groups.Select(group => new SessionGroup(group.Id, group.Name)).ToArray()));
    }
}

public sealed record SessionUser(
    Guid Id,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<SessionGroup> Groups);

public sealed record SessionGroup(Guid Id, string Name);
