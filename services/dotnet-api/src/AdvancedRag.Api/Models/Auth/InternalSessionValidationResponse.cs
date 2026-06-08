namespace AdvancedRag.Api.Models.Auth;

public sealed record InternalSessionValidationResponse(
    Guid UserId,
    string Role,
    bool IsGlobalAdmin,
    Guid OrganizationalUnitId,
    IReadOnlyList<Guid> Groups,
    long AccessScopeVersion,
    string AccessScopeHash,
    string Corpus);
