namespace AdvancedRag.Api.Models.Auth;

public sealed record InternalSessionValidationResponse(
    Guid UserId,
    string Role,
    IReadOnlyList<Guid> Groups,
    string AccessScopeHash,
    string Corpus);
