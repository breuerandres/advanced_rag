using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AdvancedRag.Api.Errors;
using AdvancedRag.Api.Middleware;
using AdvancedRag.Api.Security;
using AdvancedRag.App.Auth;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace AdvancedRag.Api.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    private const string InternalServiceTokenHeader = "X-Internal-Service-Token";

    protected IActionResult Error(
        int statusCode,
        string code,
        string message,
        IReadOnlyDictionary<string, object?>? details = null)
    {
        return StatusCode(statusCode, ErrorResponse.Create(code, message, RequestId(), details));
    }

    protected string RequestId()
    {
        return HttpContext.Items.TryGetValue(RequestIdMiddleware.ContextItemKey, out var value)
            ? value?.ToString() ?? string.Empty
            : string.Empty;
    }

    protected Guid ActorUserId()
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userId, out Guid parsed) ? parsed : Guid.Empty;
    }

    protected IReadOnlyList<string> ActorRoles()
    {
        return User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();
    }

    protected bool IsInternalTokenValid(IConfiguration configuration)
    {
        string? supplied = Request.Headers[InternalServiceTokenHeader].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(supplied))
        {
            return false;
        }

        string expected = SecretConfiguration.Read(
            configuration,
            "InternalService:Token",
            configuration["InternalService:TokenFile"] is null
                ? "InternalServiceTokenFile"
                : "InternalService:TokenFile");

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(supplied),
            Encoding.UTF8.GetBytes(expected));
    }

    protected async Task<AuthenticatedUser?> ResolveCurrentUserAsync(IAuthService auth, CancellationToken ct)
    {
        string? userIdValue = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out Guid userId)
            ? await auth.GetActiveUserAsync(userId, ct)
            : null;
    }

    protected static ClaimsPrincipal CreatePrincipal(AuthenticatedUser user)
    {
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName),
        ];
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(user.Groups.Select(group => new Claim("group", group.Id.ToString())));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }
}
