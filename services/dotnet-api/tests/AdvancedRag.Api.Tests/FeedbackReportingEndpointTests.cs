using System.Net;
using System.Net.Http.Json;
using AdvancedRag.Api.Models.Reporting;
using AdvancedRag.App.Auth;
using AdvancedRag.App.Reporting;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace AdvancedRag.Api.Tests;

public sealed class FeedbackReportingEndpointTests
    : IClassFixture<FeedbackReportingWebApplicationFactory>
{
    private readonly FeedbackReportingWebApplicationFactory _factory;

    public FeedbackReportingEndpointTests(FeedbackReportingWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ListFeedback_AsAdmin_PassesSupportedFiltersAndReturnsRows()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        LoginSession session = await LoginAsync(client, FakeAuthService.AdminEmail, "manage.localhost");
        Guid citedDocumentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Guid userId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        using HttpRequestMessage request = new(
            HttpMethod.Get,
            $"/api/reporting/feedback?negativeOnly=true&citedDocumentId={citedDocumentId}&userId={userId}&from=2026-05-01T00:00:00Z&to=2026-06-01T00:00:00Z");
        request.Headers.Host = "manage.localhost";
        request.Headers.Add("Cookie", session.SessionCookie);

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        IReadOnlyList<FeedbackReportItemResponse>? body =
            await response.Content.ReadFromJsonAsync<IReadOnlyList<FeedbackReportItemResponse>>();
        body.Should().NotBeNull();
        body!.Should().ContainSingle();
        body![0].FeedbackValue.Should().Be("down");
        _factory.Reporting.LastQuery.Should().Be(
            new FeedbackReportQuery(
                NegativeOnly: true,
                CitedDocumentId: citedDocumentId,
                UserId: userId,
                From: DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
                To: DateTimeOffset.Parse("2026-06-01T00:00:00Z")));
    }

    [Fact]
    public async Task ListFeedback_AsViewer_ReturnsForbidden()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        LoginSession session = await LoginAsync(client, FakeAuthService.TargetEmail, "manage.localhost");

        using HttpRequestMessage request = new(HttpMethod.Get, "/api/reporting/feedback");
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
        return new LoginSession(CookiePair(GetSetCookie(response, "__Host-advanced-rag-session")));
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
}

public sealed class FeedbackReportingWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly FakeAuthService _auth = new();
    public FakeFeedbackReportingService Reporting { get; } = new();

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
            services.RemoveAll<IFeedbackReportingService>();
            services.AddSingleton<IAuthService>(_auth);
            services.AddSingleton<IFeedbackReportingService>(Reporting);
        });
    }
}

public sealed class FakeFeedbackReportingService : IFeedbackReportingService
{
    public FeedbackReportQuery? LastQuery { get; private set; }

    public Task<IReadOnlyList<FeedbackReportItem>> ListFeedbackAsync(
        FeedbackReportQuery query,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        LastQuery = query;
        IReadOnlyList<FeedbackReportItem> rows =
        [
            new FeedbackReportItem(
                QueryAuditEventId: Guid.Parse("33333333-3333-3333-3333-333333333333"),
                UserId: query.UserId ?? Guid.Parse("22222222-2222-2222-2222-222222222222"),
                UserDisplayName: "Ana Gomez",
                Question: "Pregunta",
                AnswerSummary: "Respuesta resumida",
                FeedbackValue: "down",
                FeedbackComment: "No sirvio",
                FeedbackUpdatedAt: DateTimeOffset.Parse("2026-05-18T12:00:00Z"),
                CreatedAt: DateTimeOffset.Parse("2026-05-18T11:59:00Z"),
                CacheHit: false,
                RequestId: "req-report",
                Citations:
                [
                    new FeedbackReportCitation(
                        DocumentId: query.CitedDocumentId ?? Guid.Parse("11111111-1111-1111-1111-111111111111"),
                        DocumentVersionId: Guid.Parse("44444444-4444-4444-4444-444444444444"),
                        HeadingPath: ["Policy"])
                ])
        ];
        return Task.FromResult(rows);
    }
}
