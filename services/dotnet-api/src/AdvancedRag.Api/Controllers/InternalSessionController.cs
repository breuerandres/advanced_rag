using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AdvancedRag.Api.Models.Auth;
using AdvancedRag.Api.Security;
using AdvancedRag.App.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdvancedRag.Api.Controllers;

[ApiController]
[Authorize]
[Route("internal/session")]
public sealed class InternalSessionController : ApiControllerBase
{
    private const string InternalServiceTokenHeader = "X-Internal-Service-Token";
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
        if (!IsInternalTokenValid())
        {
            return Error(
                StatusCodes.Status401Unauthorized,
                "AUTH_INTERNAL_TOKEN_INVALID",
                "Internal service token is invalid.");
        }

        AuthenticatedUser? user = await ResolveCurrentUserAsync(ct);
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

    private async Task<AuthenticatedUser?> ResolveCurrentUserAsync(CancellationToken ct)
    {
        string? userIdValue = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out Guid userId)
            ? await _auth.GetActiveUserAsync(userId, ct)
            : null;
    }

    private bool IsInternalTokenValid()
    {
        string? supplied = Request.Headers[InternalServiceTokenHeader].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(supplied))
        {
            return false;
        }

        string expected = SecretConfiguration.Read(
            _configuration,
            "InternalService:Token",
            _configuration["InternalService:TokenFile"] is null
                ? "InternalServiceTokenFile"
                : "InternalService:TokenFile");

        byte[] suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
        return CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedBytes);
    }
}
