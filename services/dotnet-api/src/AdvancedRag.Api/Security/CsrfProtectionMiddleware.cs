using AdvancedRag.Api.Errors;
using AdvancedRag.Api.Middleware;

namespace AdvancedRag.Api.Security;

public sealed class CsrfProtectionMiddleware
{
    private static readonly HashSet<string> MutatingMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Delete,
        HttpMethods.Patch,
        HttpMethods.Post,
        HttpMethods.Put,
    };

    private readonly RequestDelegate _next;

    public CsrfProtectionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!MutatingMethods.Contains(context.Request.Method))
        {
            await _next(context);
            return;
        }

        var csrf = context.RequestServices.GetRequiredService<ICsrfTokenService>();
        var headerToken = context.Request.Headers[CsrfTokenService.HeaderName].FirstOrDefault();
        var cookieToken = context.Request.Cookies[CsrfTokenService.CookieName];

        if (headerToken is null || cookieToken is null || !csrf.Validate(headerToken, cookieToken))
        {
            await WriteErrorAsync(context);
            return;
        }

        await _next(context);
    }

    private static async Task WriteErrorAsync(HttpContext context)
    {
        var requestId = context.Items.TryGetValue(RequestIdMiddleware.ContextItemKey, out var value)
            ? value?.ToString() ?? string.Empty
            : string.Empty;

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(
            ErrorResponse.Create("CSRF_TOKEN_INVALID", "CSRF token is invalid.", requestId));
    }
}
