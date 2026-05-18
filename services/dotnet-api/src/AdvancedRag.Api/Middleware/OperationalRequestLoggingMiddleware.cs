using System.Collections.Concurrent;
using System.Text.Json;

namespace AdvancedRag.Api.Middleware;

public sealed class OperationalRequestLoggingMiddleware
{
    public const string ErrorCodeItemKey = "SafeErrorCode";

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> LogFileLocks = new(StringComparer.OrdinalIgnoreCase);

    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;

    public OperationalRequestLoggingMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        try
        {
            await _next(context);
        }
        finally
        {
            await WriteLogAsync(context, startedAt);
        }
    }

    private async Task WriteLogAsync(HttpContext context, DateTimeOffset startedAt)
    {
        string directory = _configuration["Logging:Directory"] ?? "/var/log/dotnet-api";
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"log-{startedAt:yyyyMMdd}.json");
        object entry = new
        {
            timestamp = startedAt.ToString("O"),
            service = "dotnet-api",
            request_id = context.Items.TryGetValue(RequestIdMiddleware.ContextItemKey, out object? requestId)
                ? requestId?.ToString()
                : null,
            origin_ip = ResolveOriginIp(context),
            route = context.Request.Path.Value,
            method = context.Request.Method,
            response_status = context.Response.StatusCode,
            safe_error_code = context.Items.TryGetValue(ErrorCodeItemKey, out object? code) ? code?.ToString() : null,
            elapsed_ms = (int)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds,
        };

        SemaphoreSlim logFileLock = LogFileLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
        await logFileLock.WaitAsync();
        try
        {
            await File.AppendAllTextAsync(path, JsonSerializer.Serialize(entry) + Environment.NewLine);
        }
        finally
        {
            logFileLock.Release();
        }
    }

    private static string ResolveOriginIp(HttpContext context)
    {
        string? forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            return forwarded.Split(',', 2)[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
