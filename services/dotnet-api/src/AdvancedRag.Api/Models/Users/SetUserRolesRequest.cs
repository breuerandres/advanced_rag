namespace AdvancedRag.Api.Models.Users;

public sealed record SetUserRolesRequest(IReadOnlyList<string>? Roles);
