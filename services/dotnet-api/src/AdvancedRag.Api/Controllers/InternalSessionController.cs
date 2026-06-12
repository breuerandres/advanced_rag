using AdvancedRag.Api.Models.Auth;
using AdvancedRag.App.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdvancedRag.Api.Controllers;

[ApiController]
[Authorize]
[Route("internal/session")]
public sealed class InternalSessionController : ApiControllerBase
{
    private readonly IAuthService _auth;
    private readonly IEffectiveAccessScopeRepository _effectiveScopes;
    private readonly IConfiguration _configuration;

    public InternalSessionController(
        IAuthService auth,
        IEffectiveAccessScopeRepository effectiveScopes,
        IConfiguration configuration)
    {
        _auth = auth;
        _effectiveScopes = effectiveScopes;
        _configuration = configuration;
    }

    [HttpGet("validate")]
    public async Task<IActionResult> ValidateAsync(CancellationToken ct)
    {
        if (!IsInternalTokenValid(_configuration))
        {
            return Error(
                StatusCodes.Status401Unauthorized,
                "AUTH_INTERNAL_TOKEN_INVALID",
                "Internal service token is invalid.");
        }

        AuthenticatedUser? user = await ResolveCurrentUserAsync(_auth, ct);
        if (user is null)
        {
            return Error(StatusCodes.Status401Unauthorized, "AUTH_REQUIRED", "Authentication required.");
        }

        EffectiveAccessScope? scope = await _effectiveScopes.FindForActiveUserAsync(user.Id, ct);
        if (scope is null)
        {
            return Error(StatusCodes.Status401Unauthorized, "AUTH_REQUIRED", "Authentication required.");
        }

        return Ok(new InternalSessionValidationResponse(
            scope.UserId,
            scope.PrimaryRole,
            scope.IsGlobalAdmin,
            scope.OrganizationalUnitId,
            scope.GroupIds,
            scope.AccessScopeVersion,
            scope.AccessScopeHash,
            scope.Corpus));
    }

}
