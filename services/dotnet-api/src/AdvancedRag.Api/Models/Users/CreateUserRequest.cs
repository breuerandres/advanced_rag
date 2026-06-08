namespace AdvancedRag.Api.Models.Users;

public sealed record CreateUserRequest(
    string Email,
    string DisplayName,
    string Password,
    IReadOnlyList<string>? Roles,
    IReadOnlyList<Guid>? GroupIds,
    Guid? OrganizationalUnitId);
