using AdvancedRag.Api.Errors;
using Microsoft.Extensions.Primitives;

namespace AdvancedRag.Api.Middleware;

public sealed class RequestIdMiddleware
{
    public const string HeaderName = "X-Request-ID";
    public const string ContextItemKey = "RequestId";

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestIdMiddleware> _logger;

    public RequestIdMiddleware(RequestDelegate next, ILogger<RequestIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = ResolveRequestId(context.Request.Headers);
        context.Items[ContextItemKey] = requestId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = requestId;
            return Task.CompletedTask;
        });

        try
        {
            await _next(context);

            if (context.Response.StatusCode == StatusCodes.Status404NotFound && !context.Response.HasStarted)
            {
                context.Items[OperationalRequestLoggingMiddleware.ErrorCodeItemKey] = "NOT_FOUND";
                await WriteErrorAsync(context, StatusCodes.Status404NotFound, ErrorResponse.NotFound(requestId));
            }
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            _logger.LogError(exception, "Unhandled request exception.");
            context.Items[OperationalRequestLoggingMiddleware.ErrorCodeItemKey] = "INTERNAL_ERROR";
            await WriteErrorAsync(
                context,
                StatusCodes.Status500InternalServerError,
                ErrorResponse.InternalError(requestId));
        }
    }

    private static string ResolveRequestId(IHeaderDictionary headers)
    {
        var candidates = new[] { HeaderName, "X-Request-Id" };

        foreach (var headerName in candidates)
        {
            if (!headers.TryGetValue(headerName, out StringValues values))
            {
                continue;
            }

            var value = values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return Guid.NewGuid().ToString("N");
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, ErrorResponse error)
    {
        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(error);
    }
}
