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
    private readonly ISessionHandoffService _handoffs;
    private readonly ICsrfTokenService _csrf;

    public AuthController(IAuthService auth, ISessionHandoffService handoffs, ICsrfTokenService csrf)
    {
        _auth = auth;
        _handoffs = handoffs;
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

    [HttpPost("session-handoffs")]
    [Authorize]
    public async Task<IActionResult> CreateSessionHandoffAsync(
        [FromBody] CreateSessionHandoffRequest request,
        CancellationToken ct)
    {
        try
        {
            SessionHandoffResult result = await _handoffs.CreateAsync(
                new CreateSessionHandoffCommand(ActorUserId(), request.Target, RequestId()),
                ct);
            return Ok(new SessionHandoffResponse(result.Target, result.HandoffCode, result.ExpiresAt));
        }
        catch (SessionHandoffException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }

    [HttpPost("session-handoffs/consume")]
    [AllowAnonymous]
    public async Task<IActionResult> ConsumeSessionHandoffAsync(
        [FromBody] ConsumeSessionHandoffRequest request,
        CancellationToken ct)
    {
        SessionHandoffConsumeResult handoff;
        try
        {
            handoff = await _handoffs.ConsumeAsync(
                new ConsumeSessionHandoffCommand(request.HandoffCode, request.Target),
                ct);
        }
        catch (SessionHandoffException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }

        AuthenticatedUser? user = await _auth.GetActiveUserAsync(handoff.UserId, ct);
        if (user is null)
        {
            return Error(StatusCodes.Status401Unauthorized, "AUTH_REQUIRED", "Authentication required.");
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

    [HttpGet("/api/session")]
    [Authorize]
    public async Task<IActionResult> GetSessionAsync(CancellationToken ct)
    {
        AuthenticatedUser? user = await ResolveCurrentUserAsync(_auth, ct);
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
}
