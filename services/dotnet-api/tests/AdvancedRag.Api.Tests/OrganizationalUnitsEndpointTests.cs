using System.Net;
using System.Net.Http.Json;
using AdvancedRag.App.Auth;
using AdvancedRag.App.Configuration;
using AdvancedRag.App.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace AdvancedRag.Api.Tests;

public sealed class OrganizationalUnitsEndpointTests
    : IClassFixture<OrganizationalUnitsWebApplicationFactory>
{
    private readonly OrganizationalUnitsWebApplicationFactory _factory;

    public OrganizationalUnitsEndpointTests(OrganizationalUnitsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ListOrganizationalUnits_ReturnsActiveTree()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        LoginSession session = await LoginAsync(client, FakeOrgUnitsAuthService.AdminEmail, "manage.localhost");

        using HttpRequestMessage request = new(HttpMethod.Get, "/api/organizational-units");
        request.Headers.Host = "manage.localhost";
        request.Headers.Add("Cookie", session.SessionCookie);

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        OrganizationalUnitResponse[]? body = await response.Content.ReadFromJsonAsync<OrganizationalUnitResponse[]>();
        body.Should().NotBeNull();
        body!.Should().Contain(unit =>
            unit.Id == FakeOrganizationalUnitService.RootId
            && unit.Name == "Empresa"
            && unit.ParentId == null
            && unit.Depth == 0
            && unit.IsActive);
        body.Should().Contain(unit =>
            unit.Id == FakeOrganizationalUnitService.ComunicacionId
            && unit.Name == "Comunicacion"
            && unit.ParentId == FakeOrganizationalUnitService.RootId
            && unit.Depth == 1
            && unit.IsActive);
        body.Should().NotContain(unit => unit.Name == "Inactive Unit");
    }

    [Fact]
    public async Task ListOrganizationalUnits_WithIncludeInactive_ReturnsInactiveUnits()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        LoginSession session = await LoginAsync(client, FakeOrgUnitsAuthService.AdminEmail, "manage.localhost");

        using HttpRequestMessage request = new(HttpMethod.Get, "/api/organizational-units?includeInactive=true");
        request.Headers.Host = "manage.localhost";
        request.Headers.Add("Cookie", session.SessionCookie);

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        OrganizationalUnitResponse[]? body = await response.Content.ReadFromJsonAsync<OrganizationalUnitResponse[]>();
        body!.Should().Contain(unit => unit.Name == "Inactive Unit" && !unit.IsActive);
    }

    [Fact]
    public async Task ListOrganizationalUnits_WithIncludeInactive_AsNonAdmin_ReturnsActiveOnly()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        LoginSession session = await LoginAsync(client, FakeOrgUnitsAuthService.EditorEmail, "manage.localhost");

        using HttpRequestMessage request = new(HttpMethod.Get, "/api/organizational-units?includeInactive=true");
        request.Headers.Host = "manage.localhost";
        request.Headers.Add("Cookie", session.SessionCookie);

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        OrganizationalUnitResponse[]? body = await response.Content.ReadFromJsonAsync<OrganizationalUnitResponse[]>();
        body!.Should().NotContain(unit => unit.Name == "Inactive Unit");
    }

    [Fact]
    public async Task CreateOrganizationalUnit_AsAdmin_CreatesChildNode()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        LoginSession session = await LoginAsync(client, FakeOrgUnitsAuthService.AdminEmail, "manage.localhost");

        using HttpResponseMessage response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/organizational-units",
            new { name = "Marketing", parentId = FakeOrganizationalUnitService.ComunicacionId },
            "manage.localhost",
            session.Csrf,
            session.SessionCookie);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        OrganizationalUnitResponse? body = await response.Content.ReadFromJsonAsync<OrganizationalUnitResponse>();
        body.Should().NotBeNull();
        body!.Name.Should().Be("Marketing");
        body.ParentId.Should().Be(FakeOrganizationalUnitService.ComunicacionId);
        body.Depth.Should().Be(2);
    }

    [Fact]
    public async Task PatchOrganizationalUnit_RenamesAndDeactivatesWithoutMovingBranch()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        LoginSession session = await LoginAsync(client, FakeOrgUnitsAuthService.AdminEmail, "manage.localhost");

        using HttpResponseMessage response = await SendJsonAsync(
            client,
            HttpMethod.Patch,
            $"/api/organizational-units/{FakeOrganizationalUnitService.ComunicacionId}",
            new { name = "Comunicacion Institucional", isActive = false },
            "manage.localhost",
            session.Csrf,
            session.SessionCookie);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        OrganizationalUnitResponse? body = await response.Content.ReadFromJsonAsync<OrganizationalUnitResponse>();
        body.Should().NotBeNull();
        body!.Name.Should().Be("Comunicacion Institucional");
        body.IsActive.Should().BeFalse();
        body.ParentId.Should().Be(FakeOrganizationalUnitService.RootId);
    }

    [Fact]
    public async Task PatchOrganizationalUnit_WithParentMoveShape_ReturnsValidationEnvelope()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        LoginSession session = await LoginAsync(client, FakeOrgUnitsAuthService.AdminEmail, "manage.localhost");

        using HttpResponseMessage response = await SendJsonAsync(
            client,
            HttpMethod.Patch,
            $"/api/organizational-units/{FakeOrganizationalUnitService.ComunicacionId}",
            new { parentId = FakeOrganizationalUnitService.OperacionesId },
            "manage.localhost",
            session.Csrf,
            session.SessionCookie);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ApiErrorEnvelope? body = await response.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        body!.Error.Code.Should().Be("VALIDATION_FAILED");
    }

    private static async Task<LoginSession> LoginAsync(HttpClient client, string email, string host)
    {
        HttpResponseMessage response = await LoginResponseAsync(client, email, host);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        CsrfState csrf = await GetCsrfAsync(client, host);
        return new LoginSession(csrf, CookiePair(GetSetCookie(response, "__Host-session")));
    }

    private static async Task<HttpResponseMessage> LoginResponseAsync(HttpClient client, string email, string host)
    {
        CsrfState csrf = await GetCsrfAsync(client, host);
        HttpRequestMessage request = new(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email, password = FakeOrgUnitsAuthService.ValidPassword }),
        };
        request.Headers.Host = host;
        request.Headers.Add("X-CSRF-Token", csrf.Token);
        request.Headers.Add("Cookie", csrf.Cookie);
        request.Headers.Add("X-Forwarded-For", $"198.51.100.{Interlocked.Increment(ref _loginIpCounter)}");
        return await client.SendAsync(request);
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
        string additionalCookie)
    {
        HttpRequestMessage request = new(method, path)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Host = host;
        request.Headers.Add("X-CSRF-Token", csrf.Token);
        request.Headers.Add("Cookie", $"{csrf.Cookie}; {additionalCookie}");
        return await client.SendAsync(request);
    }

    private static string GetSetCookie(HttpResponseMessage response, string cookieName)
    {
        response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values).Should().BeTrue();
        return values!.Single(value => value.StartsWith(cookieName, StringComparison.Ordinal));
    }

    private static string CookiePair(string setCookie) => setCookie.Split(';', 2)[0];

    private static int _loginIpCounter;

    private sealed record CsrfState(string Token, string Cookie);

    private sealed record LoginSession(CsrfState Csrf, string SessionCookie);

    private sealed record OrganizationalUnitResponse(
        Guid Id,
        string Name,
        Guid? ParentId,
        int Depth,
        bool IsActive);

    private sealed record ApiErrorEnvelope(ApiErrorBody Error);

    private sealed record ApiErrorBody(string Code, string Message, Dictionary<string, object> Details, string RequestId);
}

public sealed class OrganizationalUnitsWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly FakeOrgUnitsAuthService _auth = new();
    private readonly string _openAiApiKeyFile;
    private readonly string _internalServiceTokenFile;

    public OrganizationalUnitsWebApplicationFactory()
    {
        _openAiApiKeyFile = Path.GetTempFileName();
        _internalServiceTokenFile = Path.GetTempFileName();
        File.WriteAllText(_openAiApiKeyFile, "configured-openai-key-placeholder");
        File.WriteAllText(_internalServiceTokenFile, "configured-internal-service-token-placeholder");
    }

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
                ["CUSTOMER_TIMEZONE"] = "America/Argentina/Buenos_Aires",
                ["OPENAI_CHAT_MODEL"] = "gpt-4.1-nano",
                ["OPENAI_EMBEDDING_MODEL"] = "text-embedding-3-small",
                ["OPENAI_EMBEDDING_DIMENSIONS"] = "1536",
                ["DEFAULT_MONTHLY_AI_BUDGET_USD"] = "5",
                ["RAG_SEMANTIC_CACHE_TTL_HOURS"] = "24",
                ["RAG_SEMANTIC_CACHE_SIMILARITY_THRESHOLD"] = "0.90",
                ["OPENAI_API_KEY_FILE"] = _openAiApiKeyFile,
                ["INTERNAL_SERVICE_TOKEN_FILE"] = _internalServiceTokenFile,
                ["Jwt:SigningKeysJson"] = "configured-jwt-placeholder",
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAuthService>();
            services.RemoveAll<IOrganizationalUnitService>();
            services.RemoveAll<ITenantConfigService>();
            services.AddSingleton<IAuthService>(_auth);
            services.AddSingleton<IOrganizationalUnitService, FakeOrganizationalUnitService>();
            services.AddSingleton<ITenantConfigService, FakeTenantConfigService>();
        });
    }
}

public sealed class FakeOrgUnitsAuthService : IAuthService
{
    public static readonly Guid AdminUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public const string AdminEmail = "admin@example.com";
    public static readonly Guid EditorUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public const string EditorEmail = "editor@example.com";
    public const string ValidPassword = "password";

    public Task<AuthenticatedUser?> AuthenticateAsync(string email, string password, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        AuthenticatedUser? user = password == ValidPassword
            ? email.Equals(AdminEmail, StringComparison.OrdinalIgnoreCase)
                ? new AuthenticatedUser(AdminUserId, AdminEmail, "Admin User", ["Admin"], [])
                : email.Equals(EditorEmail, StringComparison.OrdinalIgnoreCase)
                    ? new AuthenticatedUser(EditorUserId, EditorEmail, "Editor User", ["DocumentEditor"], [])
                    : null
            : null;
        return Task.FromResult(user);
    }

    public Task<AuthenticatedUser?> GetActiveUserAsync(Guid userId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        AuthenticatedUser? user = userId == AdminUserId
            ? new AuthenticatedUser(AdminUserId, AdminEmail, "Admin User", ["Admin"], [])
            : userId == EditorUserId
                ? new AuthenticatedUser(EditorUserId, EditorEmail, "Editor User", ["DocumentEditor"], [])
                : null;
        return Task.FromResult(user);
    }
}

public sealed class FakeOrganizationalUnitService : IOrganizationalUnitService
{
    public static readonly Guid RootId = Guid.Parse("01000000-0000-0000-0000-000000000001");
    public static readonly Guid ComunicacionId = Guid.Parse("01000000-0000-0000-0000-000000000002");
    public static readonly Guid OperacionesId = Guid.Parse("01000000-0000-0000-0000-000000000003");

    public Task<IReadOnlyList<OrganizationalUnitRecord>> ListTreeAsync(bool includeInactive, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        List<OrganizationalUnitRecord> units =
        [
            new OrganizationalUnitRecord(RootId, "Empresa", null, 0, true),
            new OrganizationalUnitRecord(ComunicacionId, "Comunicacion", RootId, 1, true),
        ];
        if (includeInactive)
        {
            units.Add(new OrganizationalUnitRecord(OperacionesId, "Inactive Unit", RootId, 1, false));
        }

        return Task.FromResult<IReadOnlyList<OrganizationalUnitRecord>>(units);
    }

    public Task<OrganizationalUnitRecord> CreateAsync(CreateOrganizationalUnitCommand command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new OrganizationalUnitRecord(
            Guid.Parse("01000000-0000-0000-0000-000000000004"),
            command.Name,
            command.ParentId,
            2,
            true));
    }

    public Task<OrganizationalUnitRecord> UpdateAsync(UpdateOrganizationalUnitCommand command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new OrganizationalUnitRecord(
            command.Id,
            command.Name ?? "Comunicacion",
            RootId,
            1,
            command.IsActive ?? true));
    }
}
