using AdvancedRag.Api.Errors;
using AdvancedRag.Api.Middleware;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace AdvancedRag.Api.Security;

public static class ApiCookieAuthEvents
{
    public static Task WriteUnauthorizedAsync(RedirectContext<CookieAuthenticationOptions> context)
    {
        return WriteErrorAsync(
            context.HttpContext,
            StatusCodes.Status401Unauthorized,
            "AUTH_REQUIRED",
            "Authentication required.");
    }

    public static Task WriteForbiddenAsync(RedirectContext<CookieAuthenticationOptions> context)
    {
        return WriteErrorAsync(
            context.HttpContext,
            StatusCodes.Status403Forbidden,
            "AUTH_FORBIDDEN",
            "Forbidden.");
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string code, string message)
    {
        var requestId = context.Items.TryGetValue(RequestIdMiddleware.ContextItemKey, out var value)
            ? value?.ToString() ?? string.Empty
            : string.Empty;

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(ErrorResponse.Create(code, message, requestId));
    }
}
