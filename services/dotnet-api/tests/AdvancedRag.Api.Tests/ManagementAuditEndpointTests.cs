using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AdvancedRag.App.Audit;
using AdvancedRag.App.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace AdvancedRag.Api.Tests;

public sealed class ManagementAuditEndpointTests
    : IClassFixture<ManagementAuditWebApplicationFactory>
{
    private readonly ManagementAuditWebApplicationFactory _factory;

    public ManagementAuditEndpointTests(ManagementAuditWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ListEvents_AsAdmin_ReturnsFunctionalAuditRows()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        LoginSession session = await LoginAsync(client, FakeAuthService.AdminEmail, "manage.localhost");

        using HttpRequestMessage request = new(
            HttpMethod.Get,
            "/api/audit/events?eventType=document.created&entityType=document&limit=25");
        request.Headers.Host = "manage.localhost";
        request.Headers.Add("Cookie", session.SessionCookie);

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        IReadOnlyList<ManagementAuditEventResponse>? body =
            await response.Content.ReadFromJsonAsync<IReadOnlyList<ManagementAuditEventResponse>>();
        body.Should().NotBeNull();
        IReadOnlyList<ManagementAuditEventResponse> items =
            body ?? throw new InvalidOperationException("Audit response body was null.");
        items.Should().ContainSingle();
        ManagementAuditEventResponse item = items.Single();
        item.EventType.Should().Be("document.created");
        item.EventLabel.Should().Be("Documento creado");
        item.ActorDisplayName.Should().Be("Admin User");
        item.RequestId.Should().Be("req-audit");
        item.Details["documentId"].GetString().Should().Be("55555555-5555-5555-5555-555555555555");
        _factory.Audit.LastQuery.Should().Be(
            new ManagementAuditQuery(
                EventType: "document.created",
                EntityType: "document",
                ActorUserId: null,
                From: null,
                To: null,
                Limit: 25));
    }

    [Fact]
    public async Task ListEvents_AsViewer_ReturnsForbidden()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        LoginSession session = await LoginAsync(client, FakeAuthService.TargetEmail, "manage.localhost");

        using HttpRequestMessage request = new(HttpMethod.Get, "/api/audit/events");
        request.Headers.Host = "manage.localhost";
        request.Headers.Add("Cookie", session.SessionCookie);

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<LoginSession> LoginAsync(HttpClient client, string email, string host)
    {
        CsrfState csrf = await GetCsrfAsync(client, host);
        using HttpResponseMessage response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/auth/login",
            new { email, password = FakeAuthService.ValidPassword },
            host,
            csrf);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return new LoginSession(CookiePair(GetSetCookie(response, "__Host-session")));
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
        HttpMethod method,
        string path,
        object body,
        string host,
        CsrfState csrf)
    {
        HttpRequestMessage request = new(method, path)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Host = host;
        request.Headers.Add("X-CSRF-Token", csrf.Token);
        request.Headers.Add("Cookie", csrf.Cookie);
        return await client.SendAsync(request);
    }

    private static string GetSetCookie(HttpResponseMessage response, string cookieName)
    {
        response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values).Should().BeTrue();
        return values!.Single(value => value.StartsWith(cookieName, StringComparison.Ordinal));
    }

    private static string CookiePair(string setCookie) => setCookie.Split(';', 2)[0];

    private sealed record CsrfState(string Token, string Cookie);

    private sealed record LoginSession(string SessionCookie);

    private sealed record ManagementAuditEventResponse(
        Guid Id,
        Guid? ActorUserId,
        string? ActorDisplayName,
        string EventType,
        string EventLabel,
        string EntityType,
        Guid? EntityId,
        Dictionary<string, JsonElement> Details,
        string RequestId,
        DateTimeOffset CreatedAt);
}

public sealed class ManagementAuditWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly FakeAuthService _auth = new();
    public FakeManagementAuditService Audit { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AppDatabase"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
                ["Csrf:SigningKey"] = "local-test-csrf-signing-key-with-enough-entropy",
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAuthService>();
            services.RemoveAll<IManagementAuditService>();
            services.AddSingleton<IAuthService>(_auth);
            services.AddSingleton<IManagementAuditService>(Audit);
        });
    }
}

public sealed class FakeManagementAuditService : IManagementAuditService
{
    public ManagementAuditQuery? LastQuery { get; private set; }

    public Task<IReadOnlyList<ManagementAuditEvent>> ListEventsAsync(
        ManagementAuditQuery query,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        LastQuery = query;

        IReadOnlyList<ManagementAuditEvent> rows =
        [
            new ManagementAuditEvent(
                Id: Guid.Parse("99999999-9999-9999-9999-999999999999"),
                ActorUserId: FakeAuthService.AdminUserId,
                ActorDisplayName: "Admin User",
                EventType: "document.created",
                EntityType: "document",
                EntityId: Guid.Parse("55555555-5555-5555-5555-555555555555"),
                Details: new Dictionary<string, object?>
                {
                    ["documentId"] = "55555555-5555-5555-5555-555555555555",
                },
                RequestId: "req-audit",
                CreatedAt: DateTimeOffset.Parse("2026-05-20T12:00:00Z"))
        ];

        return Task.FromResult(rows);
    }
}
