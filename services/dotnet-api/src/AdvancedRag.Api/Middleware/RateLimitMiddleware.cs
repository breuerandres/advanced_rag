using System.Collections.Concurrent;
using System.Text.Json;
using AdvancedRag.Api.Errors;

namespace AdvancedRag.Api.Middleware;

public sealed class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly FixedWindowRateLimiter _limiter;

    public RateLimitMiddleware(RequestDelegate next, FixedWindowRateLimiter limiter)
    {
        _next = next;
        _limiter = limiter;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        IReadOnlyList<RateLimitRule> rules = await ResolveRulesAsync(context);
        foreach (RateLimitRule rule in rules)
        {
            if (!_limiter.Allow(rule.Key, rule.Limit, rule.Window))
            {
                string requestId = context.Items.TryGetValue(RequestIdMiddleware.ContextItemKey, out object? value)
                    ? value?.ToString() ?? string.Empty
                    : string.Empty;
                context.Items[OperationalRequestLoggingMiddleware.ErrorCodeItemKey] = rule.Code;
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(ErrorResponse.Create(
                    rule.Code,
                    "Request rate limit exceeded.",
                    requestId,
                    new Dictionary<string, object?>
                    {
                        ["limit"] = rule.Limit,
                        ["windowSeconds"] = (int)rule.Window.TotalSeconds,
                    }));
                return;
            }
        }

        await _next(context);
    }

    private static async Task<IReadOnlyList<RateLimitRule>> ResolveRulesAsync(HttpContext context)
    {
        PathString path = context.Request.Path;
        if (HttpMethods.IsPost(context.Request.Method) && path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            LoginRateLimitSubject subject = await ReadLoginSubjectAsync(context);
            string ip = ResolveOriginIp(context);
            List<RateLimitRule> rules =
            [
                new RateLimitRule($"login:ip:{ip}", 5, TimeSpan.FromMinutes(1), "LOGIN_IP_RATE_LIMITED"),
            ];

            if (!string.IsNullOrWhiteSpace(subject.Email))
            {
                rules.Add(new RateLimitRule(
                    $"login:user:{subject.Email}",
                    10,
                    TimeSpan.FromMinutes(15),
                    "LOGIN_USER_RATE_LIMITED"));
            }

            return rules;
        }

        if (HttpMethods.IsPost(context.Request.Method) && path.Equals("/api/documents/imports/extract", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new RateLimitRule(
                $"import:user:{ResolveUserId(context)}",
                10,
                TimeSpan.FromHours(1),
                "IMPORT_RATE_LIMITED"),
            ];
        }

        if (HttpMethods.IsPost(context.Request.Method) && path.Equals("/api/viewer/exchange", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new RateLimitRule(
                $"viewer-exchange:{ResolveUserId(context)}:{ResolveOriginIp(context)}",
                30,
                TimeSpan.FromMinutes(1),
                "VIEWER_EXCHANGE_RATE_LIMITED"),
            ];
        }

        return [];
    }

    private static async Task<LoginRateLimitSubject> ReadLoginSubjectAsync(HttpContext context)
    {
        context.Request.EnableBuffering();
        try
        {
            LoginRateLimitSubject? subject = await context.Request.ReadFromJsonAsync<LoginRateLimitSubject>();
            return subject is null
                ? new LoginRateLimitSubject(null)
                : new LoginRateLimitSubject(subject.Email?.Trim().ToLowerInvariant());
        }
        catch (JsonException)
        {
            return new LoginRateLimitSubject(null);
        }
        finally
        {
            context.Request.Body.Position = 0;
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

    private static string ResolveUserId(HttpContext context)
    {
        return context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? "anonymous";
    }

    private sealed record LoginRateLimitSubject(string? Email);

    private sealed record RateLimitRule(string Key, int Limit, TimeSpan Window, string Code);
}

public sealed class FixedWindowRateLimiter
{
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, Counter> _counters = new();

    public FixedWindowRateLimiter(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public bool Allow(string key, int limit, TimeSpan window)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        Counter next = _counters.AddOrUpdate(
            key,
            _ => new Counter(now, 1),
            (_, existing) => now - existing.StartedAt >= window
                ? new Counter(now, 1)
                : existing with { Count = existing.Count + 1 });

        return next.Count <= limit;
    }

    private sealed record Counter(DateTimeOffset StartedAt, int Count);
}
