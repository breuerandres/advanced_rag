using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using System.Net.Http.Headers;

namespace AdvancedRag.Api.Tests;

public sealed class RateLimitEndpointTests : IClassFixture<AuthWebApplicationFactory>
{
    private readonly AuthWebApplicationFactory _factory;

    public RateLimitEndpointTests(AuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_AfterFiveAttemptsFromSameIp_IsRateLimited()
    {
        using HttpClient client = _factory.CreateClient();
        CsrfState csrf = await GetCsrfAsync(client, "manage.localhost");
        string forwardedFor = $"203.0.113.{Random.Shared.Next(1, 200)}";

        for (int attempt = 0; attempt < 5; attempt++)
        {
            using HttpResponseMessage allowed = await SendJsonAsync(
                client,
                "/api/auth/login",
                new { Email = $"missing-{attempt}@example.com", Password = "wrong" },
                csrf,
                forwardedFor: forwardedFor);
            allowed.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        using HttpResponseMessage limited = await SendJsonAsync(
            client,
            "/api/auth/login",
            new { Email = "missing-final@example.com", Password = "wrong" },
            csrf,
            forwardedFor: forwardedFor);

        limited.StatusCode.Should().Be((HttpStatusCode)429);
        ApiErrorEnvelope? body = await limited.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        body!.Error.Code.Should().Be("LOGIN_IP_RATE_LIMITED");
    }

    [Fact]
    public async Task Login_AfterTenAttemptsForSameUser_IsRateLimited()
    {
        using HttpClient client = _factory.CreateClient();
        CsrfState csrf = await GetCsrfAsync(client, "manage.localhost");
        string email = $"shared-{Guid.NewGuid():N}@example.com";

        for (int attempt = 0; attempt < 10; attempt++)
        {
            using HttpResponseMessage allowed = await SendJsonAsync(
                client,
                "/api/auth/login",
                new { Email = email, Password = $"wrong-{attempt}" },
                csrf,
                forwardedFor: $"10.0.0.{attempt + 1}");
            allowed.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        using HttpResponseMessage limited = await SendJsonAsync(
            client,
            "/api/auth/login",
            new { Email = email, Password = "wrong-final" },
            csrf,
            forwardedFor: "10.0.0.200");

        limited.StatusCode.Should().Be((HttpStatusCode)429);
        ApiErrorEnvelope? body = await limited.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        body!.Error.Code.Should().Be("LOGIN_USER_RATE_LIMITED");
    }

    [Fact]
    public async Task ImportExtraction_AfterTenRequestsPerUser_IsRateLimited()
    {
        using HttpClient client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
        });
        LoginSession session = await LoginAsync(client, "manage.localhost");

        for (int attempt = 0; attempt < 10; attempt++)
        {
            using HttpResponseMessage allowed = await SendImportAsync(client, session);
            allowed.StatusCode.Should().NotBe((HttpStatusCode)429);
        }

        using HttpResponseMessage limited = await SendImportAsync(client, session);

        limited.StatusCode.Should().Be((HttpStatusCode)429);
        ApiErrorEnvelope? body = await limited.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        body!.Error.Code.Should().Be("IMPORT_RATE_LIMITED");
    }

    [Fact]
    public async Task ViewerExchange_AfterThirtyAttempts_IsRateLimited()
    {
        using HttpClient client = _factory.CreateClient();
        CsrfState csrf = await GetCsrfAsync(client, "docs.localhost");

        for (int attempt = 0; attempt < 30; attempt++)
        {
            using HttpResponseMessage allowed = await SendJsonAsync(
                client,
                "/api/viewer/exchange",
                new { Code = $"invalid-{attempt}" },
                csrf,
                host: "docs.localhost");
            allowed.StatusCode.Should().NotBe((HttpStatusCode)429);
        }

        using HttpResponseMessage limited = await SendJsonAsync(
            client,
            "/api/viewer/exchange",
            new { Code = "invalid-final" },
            csrf,
            host: "docs.localhost");

        limited.StatusCode.Should().Be((HttpStatusCode)429);
        ApiErrorEnvelope? body = await limited.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        body!.Error.Code.Should().Be("VIEWER_EXCHANGE_RATE_LIMITED");
    }

    private static async Task<CsrfState> GetCsrfAsync(HttpClient client, string host)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/csrf");
        request.Headers.Host = host;

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.TryGetValues("X-CSRF-Token", out IEnumerable<string>? tokenValues).Should().BeTrue();
        string token = tokenValues!.Single();
        return new CsrfState(token, CookiePair(GetSetCookie(response, "__Host-CSRF")));
    }

    private static async Task<HttpResponseMessage> SendJsonAsync(
        HttpClient client,
        string path,
        object body,
        CsrfState csrf,
        string forwardedFor = "203.0.113.10",
        string host = "manage.localhost",
        string? additionalCookie = null)
    {
        HttpRequestMessage request = new(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Host = host;
        request.Headers.Add("X-CSRF-Token", csrf.Token);
        request.Headers.Add("Cookie", additionalCookie is null ? csrf.Cookie : $"{csrf.Cookie}; {additionalCookie}");
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        return await client.SendAsync(request);
    }

    private static async Task<LoginSession> LoginAsync(HttpClient client, string host)
    {
        CsrfState csrf = await GetCsrfAsync(client, host);
        using HttpResponseMessage response = await SendJsonAsync(
            client,
            "/api/auth/login",
            new { Email = AuthWebApplicationFactory.TestUserEmail, Password = "Correct Horse Battery Staple 42!" },
            csrf,
            host: host);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return new LoginSession(csrf, CookiePair(GetSetCookie(response, "__Host-session")));
    }

    private static async Task<HttpResponseMessage> SendImportAsync(HttpClient client, LoginSession session)
    {
        using MultipartFormDataContent content = new();
        ByteArrayContent file = new("import body"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(file, "file", "import.txt");

        HttpRequestMessage request = new(HttpMethod.Post, "/api/documents/imports/extract")
        {
            Content = content,
        };
        request.Headers.Host = "manage.localhost";
        request.Headers.Add("X-CSRF-Token", session.Csrf.Token);
        request.Headers.Add("Cookie", $"{session.Csrf.Cookie}; {session.SessionCookie}");
        return await client.SendAsync(request);
    }

    private static string GetSetCookie(HttpResponseMessage response, string cookieName)
    {
        response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values).Should().BeTrue();
        return values!.Single(value => value.StartsWith(cookieName, StringComparison.Ordinal));
    }

    private static string CookiePair(string setCookie)
    {
        return setCookie.Split(';', 2)[0];
    }

    private sealed record CsrfState(string Token, string Cookie);

    private sealed record LoginSession(CsrfState Csrf, string SessionCookie);

    private sealed record ApiErrorEnvelope(ApiErrorBody Error);

    private sealed record ApiErrorBody(string Code, string Message, Dictionary<string, object> Details, string RequestId);
}
