using System.Security.Claims;
using AdvancedRag.Api.Models.Auth;
using AdvancedRag.Api.Security;
using AdvancedRag.App.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdvancedRag.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ApiControllerBase
{
    private readonly IAuthService _auth;
    private readonly ICsrfTokenService _csrf;

    public AuthController(IAuthService auth, ICsrfTokenService csrf)
    {
        _auth = auth;
        _csrf = csrf;
    }

    [HttpGet("/api/csrf")]
    [AllowAnonymous]
    public ActionResult<CsrfResponse> GetCsrfToken()
    {
        string token = _csrf.CreateToken();
        Response.Cookies.Append(
            CsrfTokenService.CookieName,
            token,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/",
            });
        Response.Headers[CsrfTokenService.HeaderName] = token;
        return Ok(new CsrfResponse("ok"));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request, CancellationToken ct)
    {
        AuthenticatedUser? user = await _auth.AuthenticateAsync(request.Email, request.Password, ct);
        if (user is null)
        {
            return Error(StatusCodes.Status401Unauthorized, "AUTH_REQUIRED", "Invalid credentials.");
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            CreatePrincipal(user),
            new AuthenticationProperties
            {
                IsPersistent = false,
                IssuedUtc = DateTimeOffset.UtcNow,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
            });

        return Ok(SessionResponse.FromUser(user));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> LogoutAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new LogoutResponse("ok"));
    }

    [HttpPost("chat-token")]
    [Authorize]
    public async Task<IActionResult> IssueChatTokenAsync(
        [FromServices] IChatTokenIssuer tokenIssuer,
        CancellationToken ct)
    {
        AuthenticatedUser? user = await ResolveCurrentUserAsync(ct);
        if (user is null)
        {
            return Error(StatusCodes.Status401Unauthorized, "AUTH_REQUIRED", "Authentication required.");
        }

        IssuedChatToken issuedToken = tokenIssuer.Issue(user);
        Response.Cookies.Append(
            ChatTokenIssuer.CookieName,
            issuedToken.Token,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/",
                Expires = issuedToken.ExpiresAt,
            });

        return Ok(new ChatTokenResponse(issuedToken.ExpiresAt));
    }

    [HttpGet("/api/session")]
    [Authorize]
    public async Task<IActionResult> GetSessionAsync(CancellationToken ct)
    {
        AuthenticatedUser? user = await ResolveCurrentUserAsync(ct);
        return user is null
            ? Error(StatusCodes.Status401Unauthorized, "AUTH_REQUIRED", "Authentication required.")
            : Ok(SessionResponse.FromUser(user));
    }

    [HttpGet("/.well-known/jwks.json")]
    [AllowAnonymous]
    public IActionResult GetJwks([FromServices] JwtSigningKeyStore keys)
    {
        return Ok(keys.GetJwks());
    }

    private async Task<AuthenticatedUser?> ResolveCurrentUserAsync(CancellationToken ct)
    {
        string? userIdValue = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId)
            ? await _auth.GetActiveUserAsync(userId, ct)
            : null;
    }

    private static ClaimsPrincipal CreatePrincipal(AuthenticatedUser user)
    {
        List<Claim> claims = new()
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName),
        };
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(user.Groups.Select(group => new Claim("group", group.Id.ToString())));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }
}
