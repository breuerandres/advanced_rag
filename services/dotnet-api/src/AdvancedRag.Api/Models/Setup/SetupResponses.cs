using AdvancedRag.App.Setup;
using AdvancedRag.App.Users;

namespace AdvancedRag.Api.Models.Setup;

public sealed record SetupStatusResponse(
    bool SetupRequired,
    bool AdminExists,
    bool DatabaseReady,
    IReadOnlyList<string> RequiredRoles)
{
    public static SetupStatusResponse FromStatus(SetupStatus status)
    {
        return new SetupStatusResponse(
            status.SetupRequired,
            status.AdminExists,
            true,
            status.RequiredRoles);
    }
}

public sealed record SetupAdminResponse(SetupUserResponse User)
{
    public static SetupAdminResponse FromResult(FirstAdminResult result)
    {
        return new SetupAdminResponse(SetupUserResponse.FromUser(result.User));
    }
}

public sealed record SetupUserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles)
{
    public static SetupUserResponse FromUser(UserManagementUser user)
    {
        return new SetupUserResponse(user.Id, user.Email, user.DisplayName, user.Roles);
    }
}
