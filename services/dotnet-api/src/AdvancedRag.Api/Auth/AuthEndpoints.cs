using System.Security.Claims;
using AdvancedRag.Api.Errors;
using AdvancedRag.Api.Middleware;
using AdvancedRag.Api.Security;
using AdvancedRag.App.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AdvancedRag.Api.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/csrf", GetCsrfToken);
        app.MapPost("/api/auth/login", LoginAsync);
        app.MapPost("/api/auth/logout", LogoutAsync).RequireAuthorization();
        app.MapPost("/api/auth/chat-token", IssueChatTokenAsync).RequireAuthorization();
        app.MapGet("/api/session", GetSessionAsync).RequireAuthorization();
        app.MapGet("/.well-known/jwks.json", (JwtSigningKeyStore keys) => Results.Json(keys.GetJwks()));
        return app;
    }

    private static Ok<CsrfResponse> GetCsrfToken(HttpContext context, ICsrfTokenService csrf)
    {
        var token = csrf.CreateToken();
        context.Response.Cookies.Append(
            CsrfTokenService.CookieName,
            token,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/",
            });
        context.Response.Headers[CsrfTokenService.HeaderName] = token;
        return TypedResults.Ok(new CsrfResponse("ok"));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IAuthService auth,
        HttpContext context,
        CancellationToken ct)
    {
        var user = await auth.AuthenticateAsync(request.Email, request.Password, ct);
        if (user is null)
        {
            return Error(
                context,
                StatusCodes.Status401Unauthorized,
                "AUTH_REQUIRED",
                "Invalid credentials.");
        }

        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            CreatePrincipal(user),
            new AuthenticationProperties
            {
                IsPersistent = false,
                IssuedUtc = DateTimeOffset.UtcNow,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
            });

        return TypedResults.Ok(SessionResponse.FromUser(user));
    }

    private static async Task<IResult> LogoutAsync(HttpContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return TypedResults.Ok(new LogoutResponse("ok"));
    }

    private static async Task<IResult> GetSessionAsync(
        HttpContext context,
        IAuthService auth,
        CancellationToken ct)
    {
        var user = await ResolveCurrentUserAsync(context, auth, ct);
        return user is null
            ? Error(context, StatusCodes.Status401Unauthorized, "AUTH_REQUIRED", "Authentication required.")
            : TypedResults.Ok(SessionResponse.FromUser(user));
    }

    private static async Task<IResult> IssueChatTokenAsync(
        HttpContext context,
        IAuthService auth,
        IChatTokenIssuer tokenIssuer,
        CancellationToken ct)
    {
        var user = await ResolveCurrentUserAsync(context, auth, ct);
        if (user is null)
        {
            return Error(context, StatusCodes.Status401Unauthorized, "AUTH_REQUIRED", "Authentication required.");
        }

        var issuedToken = tokenIssuer.Issue(user);
        context.Response.Cookies.Append(
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

        return TypedResults.Ok(new ChatTokenResponse(issuedToken.ExpiresAt));
    }

    private static ClaimsPrincipal CreatePrincipal(AuthenticatedUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName),
        };
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(user.Groups.Select(group => new Claim("group", group.Id.ToString())));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    private static async Task<AuthenticatedUser?> ResolveCurrentUserAsync(
        HttpContext context,
        IAuthService auth,
        CancellationToken ct)
    {
        var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId)
            ? await auth.GetActiveUserAsync(userId, ct)
            : null;
    }

    private static IResult Error(HttpContext context, int statusCode, string code, string message)
    {
        var requestId = context.Items.TryGetValue(RequestIdMiddleware.ContextItemKey, out var value)
            ? value?.ToString() ?? string.Empty
            : string.Empty;

        return Results.Json(
            ErrorResponse.Create(code, message, requestId),
            statusCode: statusCode);
    }
}

public sealed record LoginRequest(string Email, string Password);

public sealed record CsrfResponse(string Status);

public sealed record LogoutResponse(string Status);

public sealed record ChatTokenResponse(DateTimeOffset ExpiresAt);

public sealed record SessionResponse(SessionUser User)
{
    public static SessionResponse FromUser(AuthenticatedUser user)
    {
        return new SessionResponse(
            new SessionUser(
                user.Id,
                user.Email,
                user.DisplayName,
                user.Roles,
                user.Groups.Select(group => new SessionGroup(group.Id, group.Name)).ToArray()));
    }
}

public sealed record SessionUser(
    Guid Id,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<SessionGroup> Groups);

public sealed record SessionGroup(Guid Id, string Name);
