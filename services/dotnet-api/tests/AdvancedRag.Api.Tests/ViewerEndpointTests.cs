using System.Net;
using System.Net.Http.Json;
using AdvancedRag.App.Auth;
using AdvancedRag.App.Viewer;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace AdvancedRag.Api.Tests;

public sealed class ViewerEndpointTests : IClassFixture<ViewerWebApplicationFactory>
{
    private readonly ViewerWebApplicationFactory _factory;

    public ViewerEndpointTests(ViewerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateLink_ForChat_ReturnsDocsOpenUrl()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        LoginSession session = await LoginAsync(client, ViewerFakeAuthService.TargetEmail, "chat.localhost");

        using HttpResponseMessage response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/viewer/links",
            new { instructionId = FakeViewerAccessService.PublishedInstructionId, purpose = "chat" },
            "chat.localhost",
            session.Csrf,
            session.SessionCookie);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ViewerLinkResponse? body = await response.Content.ReadFromJsonAsync<ViewerLinkResponse>();
        body!.Url.Should().StartWith("https://docs.client.com/open?code=");
        _factory.Viewer.LastCreateCommand!.Purpose.Should().Be("chat");
    }

    [Fact]
    public async Task CreateLink_ForChatRejectsNonPublishedDocuments()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        LoginSession session = await LoginAsync(client, ViewerFakeAuthService.TargetEmail, "chat.localhost");

        using HttpResponseMessage response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/viewer/links",
            new { instructionId = FakeViewerAccessService.DraftInstructionId, purpose = "chat" },
            "chat.localhost",
            session.Csrf,
            session.SessionCookie);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        ApiErrorEnvelope? body = await response.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        body!.Error.Code.Should().Be("AUTH_FORBIDDEN");
    }

    [Fact]
    public async Task CreateLink_ForManagementAllowsDraftReviewAndPublishedForManagers()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        LoginSession session = await LoginAsync(client, ViewerFakeAuthService.ManagerEmail, "manage.localhost");

        using HttpResponseMessage response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/viewer/links",
            new { instructionId = FakeViewerAccessService.DraftInstructionId, purpose = "management" },
            "manage.localhost",
            session.Csrf,
            session.SessionCookie);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.Viewer.LastCreateCommand!.Purpose.Should().Be("management");
    }

    [Fact]
    public async Task ExchangeCode_SetsSecureHostOnlyViewerTokenCookie()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        CsrfState csrf = await GetCsrfAsync(client, "docs.localhost");

        using HttpResponseMessage response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/viewer/exchange",
            new { code = FakeViewerAccessService.ValidCode },
            "docs.localhost",
            csrf);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        string viewerCookie = GetSetCookie(response, "__Host-viewer-token");
        string normalizedCookie = viewerCookie.ToLowerInvariant();
        normalizedCookie.Should().Contain("httponly", Exactly.Once());
        normalizedCookie.Should().Contain("secure", Exactly.Once());
        normalizedCookie.Should().Contain("samesite=strict", Exactly.Once());
        normalizedCookie.Should().Contain("path=/", Exactly.Once());
        normalizedCookie.Should().NotContain("domain=");
    }

    [Theory]
    [InlineData(FakeViewerAccessService.ExpiredCode, HttpStatusCode.Gone, "VIEWER_CODE_EXPIRED")]
    [InlineData(FakeViewerAccessService.UsedCode, HttpStatusCode.Gone, "VIEWER_CODE_USED")]
    [InlineData(FakeViewerAccessService.UnauthorizedCode, HttpStatusCode.Forbidden, "AUTH_FORBIDDEN")]
    public async Task ExchangeCode_ReturnsSafeErrors(string code, HttpStatusCode status, string errorCode)
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        CsrfState csrf = await GetCsrfAsync(client, "docs.localhost");

        using HttpResponseMessage response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/viewer/exchange",
            new { code },
            "docs.localhost",
            csrf);

        response.StatusCode.Should().Be(status);
        ApiErrorEnvelope? body = await response.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        body!.Error.Code.Should().Be(errorCode);
    }

    [Fact]
    public async Task GetDocument_WithViewerTokenReturnsDocumentWithinTokenTtl()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        CsrfState csrf = await GetCsrfAsync(client, "docs.localhost");
        using HttpResponseMessage exchange = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/viewer/exchange",
            new { code = FakeViewerAccessService.ValidCode },
            "docs.localhost",
            csrf);
        string viewerCookie = CookiePair(GetSetCookie(exchange, "__Host-viewer-token"));

        using HttpRequestMessage request = new(HttpMethod.Get, "/api/viewer/document");
        request.Headers.Host = "docs.localhost";
        request.Headers.Add("Cookie", viewerCookie);

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ViewerDocumentResponse? body = await response.Content.ReadFromJsonAsync<ViewerDocumentResponse>();
        body!.Title.Should().Be("Published procedure");
        body.ContentHtml.Should().Contain("Contenido publicado");
    }

    private static async Task<LoginSession> LoginAsync(HttpClient client, string email, string host)
    {
        CsrfState csrf = await GetCsrfAsync(client, host);
        using HttpResponseMessage response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/auth/login",
            new { email, password = ViewerFakeAuthService.ValidPassword },
            host,
            csrf);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return new LoginSession(csrf, CookiePair(GetSetCookie(response, "__Host-advanced-rag-session")));
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
        CsrfState csrf,
        string? additionalCookie = null)
    {
        HttpRequestMessage request = new(method, path)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Host = host;
        request.Headers.Add("X-CSRF-Token", csrf.Token);
        request.Headers.Add("Cookie", additionalCookie is null ? csrf.Cookie : $"{csrf.Cookie}; {additionalCookie}");
        return await client.SendAsync(request);
    }

    private static string GetSetCookie(HttpResponseMessage response, string cookieName)
    {
        response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values).Should().BeTrue();
        return values!.Single(value => value.StartsWith(cookieName, StringComparison.Ordinal));
    }

    private static string CookiePair(string setCookie) => setCookie.Split(';', 2)[0];

    private sealed record CsrfState(string Token, string Cookie);

    private sealed record LoginSession(CsrfState Csrf, string SessionCookie);

    private sealed record ViewerLinkResponse(string Url, DateTimeOffset ExpiresAt);

    private sealed record ViewerDocumentResponse(
        Guid InstructionId,
        Guid InstructionVersionId,
        string Title,
        string State,
        string InstructionType,
        string Audience,
        string ContentHtml,
        DateTimeOffset TokenExpiresAt);

    private sealed record ApiErrorEnvelope(ApiErrorBody Error);

    private sealed record ApiErrorBody(string Code, string Message, Dictionary<string, object> Details, string RequestId);
}

public sealed class ViewerWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly ViewerFakeAuthService _auth = new();
    public FakeViewerAccessService Viewer { get; } = new();

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
            services.RemoveAll<IViewerAccessService>();
            services.AddSingleton<IAuthService>(_auth);
            services.AddSingleton<IViewerAccessService>(Viewer);
        });
    }
}

public sealed class FakeViewerAccessService : IViewerAccessService
{
    public static readonly Guid PublishedInstructionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid DraftInstructionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public const string ValidCode = "valid-code";
    public const string ExpiredCode = "expired-code";
    public const string UsedCode = "used-code";
    public const string UnauthorizedCode = "unauthorized-code";

    public CreateViewerLinkCommand? LastCreateCommand { get; private set; }

    public Task<ViewerLinkResult> CreateLinkAsync(CreateViewerLinkCommand command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        LastCreateCommand = command;
        if (command.Purpose == "chat" && command.InstructionId != PublishedInstructionId)
        {
            throw new ViewerAccessException("AUTH_FORBIDDEN", 403, "Viewer link is not allowed.");
        }

        if (command.Purpose == "management"
            && !command.Roles.Contains("Admin", StringComparer.Ordinal)
            && !command.Roles.Contains("DocumentManager", StringComparer.Ordinal))
        {
            throw new ViewerAccessException("AUTH_FORBIDDEN", 403, "Viewer link is not allowed.");
        }

        return Task.FromResult(new ViewerLinkResult(
            $"https://docs.client.com/open?code={ValidCode}",
            DateTimeOffset.UtcNow.AddSeconds(60)));
    }

    public Task<ViewerExchangeResult> ExchangeCodeAsync(ExchangeViewerCodeCommand command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return command.Code switch
        {
            ExpiredCode => throw new ViewerAccessException("VIEWER_CODE_EXPIRED", 410, "Viewer code expired."),
            UsedCode => throw new ViewerAccessException("VIEWER_CODE_USED", 410, "Viewer code already used."),
            UnauthorizedCode => throw new ViewerAccessException("AUTH_FORBIDDEN", 403, "Viewer code is not authorized."),
            _ => Task.FromResult(new ViewerExchangeResult(
                "fake-viewer-token",
                "viewer-token-id",
                PublishedInstructionId,
                ViewerFakeAuthService.TargetUserId,
                "chat",
                DateTimeOffset.UtcNow.AddMinutes(15))),
        };
    }

    public Task<ViewerDocumentResult> GetDocumentAsync(GetViewerDocumentCommand command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (command.Token != "fake-viewer-token")
        {
            throw new ViewerAccessException("AUTH_TOKEN_INVALID", 401, "Viewer token is invalid.");
        }

        return Task.FromResult(new ViewerDocumentResult(
            PublishedInstructionId,
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "Published procedure",
            "Published",
            "Policy",
            "Operations",
            "<h1>Contenido publicado</h1>",
            DateTimeOffset.UtcNow.AddMinutes(15)));
    }
}

public sealed class ViewerFakeAuthService : IAuthService
{
    public static readonly Guid TargetUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid ManagerUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public const string TargetEmail = "viewer@example.com";
    public const string ManagerEmail = "manager@example.com";
    public const string ValidPassword = "password";
    private readonly Dictionary<Guid, ViewerFakeUserState> _users = new()
    {
        [TargetUserId] = new(TargetUserId, TargetEmail, "Viewer User", true, ["Viewer"], []),
        [ManagerUserId] = new(ManagerUserId, ManagerEmail, "Manager User", true, ["DocumentManager"], []),
    };

    public Task<AuthenticatedUser?> AuthenticateAsync(string email, string password, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ViewerFakeUserState? user = _users.Values.SingleOrDefault(item =>
            item.IsActive && item.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(password == ValidPassword ? user?.ToAuthenticatedUser() : null);
    }

    public Task<AuthenticatedUser?> GetActiveUserAsync(Guid userId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(
            _users.TryGetValue(userId, out ViewerFakeUserState? user) && user.IsActive
                ? user.ToAuthenticatedUser()
                : null);
    }

    private sealed record ViewerFakeUserState(
        Guid Id,
        string Email,
        string DisplayName,
        bool IsActive,
        IReadOnlyList<string> Roles,
        IReadOnlyList<AuthGroup> Groups)
    {
        public AuthenticatedUser ToAuthenticatedUser()
        {
            return new AuthenticatedUser(Id, Email, DisplayName, Roles, Groups);
        }
    }
}
