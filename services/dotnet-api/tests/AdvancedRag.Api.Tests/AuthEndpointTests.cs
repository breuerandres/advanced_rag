using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AdvancedRag.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace AdvancedRag.Api.Tests;

public sealed class AuthEndpointTests : IClassFixture<AuthWebApplicationFactory>
{
    private const string TestPassword = "Correct Horse Battery Staple 42!";
    private readonly AuthWebApplicationFactory _factory;

    public AuthEndpointTests(AuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_WithValidCredentials_SetsSecureHostOnlySessionCookie()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var csrf = await GetCsrfAsync(client, "manage.localhost");

        using var response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/auth/login",
            new { Email = AuthWebApplicationFactory.TestUserEmail, Password = TestPassword },
            "manage.localhost",
            csrf);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var sessionCookie = GetSetCookie(response, "__Host-session");
        var normalizedSessionCookie = sessionCookie.ToLowerInvariant();
        normalizedSessionCookie.Should().Contain("httponly", Exactly.Once());
        normalizedSessionCookie.Should().Contain("secure", Exactly.Once());
        normalizedSessionCookie.Should().Contain("samesite=strict", Exactly.Once());
        normalizedSessionCookie.Should().Contain("path=/", Exactly.Once());
        normalizedSessionCookie.Should().NotContain("domain=");
    }

    [Fact]
    public async Task Logout_WithValidSession_ClearsSessionCookie()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var session = await LoginAsync(client, "manage.localhost");

        using var response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/auth/logout",
            new { },
            "manage.localhost",
            session.Csrf,
            session.SessionCookie);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var clearedCookie = GetSetCookie(response, "__Host-session");
        var normalizedClearedCookie = clearedCookie.ToLowerInvariant();
        normalizedClearedCookie.Should().Contain("expires=thu, 01 jan 1970", Exactly.Once());
        normalizedClearedCookie.Should().NotContain("domain=");
    }

    [Fact]
    public async Task MutatingRequest_WithoutCsrfToken_IsRejected()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        using var response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/auth/login",
            new { Email = AuthWebApplicationFactory.TestUserEmail, Password = TestPassword },
            "manage.localhost");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        body!.Error.Code.Should().Be("CSRF_TOKEN_INVALID");
    }

    [Fact]
    public async Task Session_WithValidSession_ReturnsCurrentUserRolesAndGroups()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var session = await LoginAsync(client, "manage.localhost");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/session");
        request.Headers.Host = "manage.localhost";
        request.Headers.Add("Cookie", session.SessionCookie);

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SessionResponse>();
        body.Should().NotBeNull();
        body!.User.Email.Should().Be(AuthWebApplicationFactory.TestUserEmail);
        body.User.Roles.Should().BeEquivalentTo(["Viewer"]);
        body.User.Groups.Should().ContainSingle(group => group.Id == AuthWebApplicationFactory.TestGroupId);
    }

    [Fact]
    public async Task InternalSessionValidate_WithValidSessionAndInternalToken_ReturnsSafeClaims()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var session = await LoginAsync(client, "chat.localhost");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/internal/session/validate");
        request.Headers.Host = "dotnet-api";
        request.Headers.Add("Cookie", session.SessionCookie);
        request.Headers.Add("X-Internal-Service-Token", AuthWebApplicationFactory.InternalServiceToken);

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<InternalSessionValidationResponse>();
        body.Should().NotBeNull();
        body!.UserId.Should().Be(AuthWebApplicationFactory.TestUserId);
        body.Role.Should().Be("Viewer");
        body.Groups.Should().BeEquivalentTo([AuthWebApplicationFactory.TestGroupId]);
        body.AccessScopeHash.Should().NotBeNullOrWhiteSpace();
        body.Corpus.Should().Be("published");
    }

    private static async Task<LoginSession> LoginAsync(HttpClient client, string host)
    {
        var csrf = await GetCsrfAsync(client, host);
        using var response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/auth/login",
            new { Email = AuthWebApplicationFactory.TestUserEmail, Password = TestPassword },
            host,
            csrf);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return new LoginSession(csrf, CookiePair(GetSetCookie(response, "__Host-session")));
    }

    private static async Task<CsrfState> GetCsrfAsync(HttpClient client, string host)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/csrf");
        request.Headers.Host = host;

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.TryGetValues("X-CSRF-Token", out var tokenValues).Should().BeTrue();
        var token = tokenValues!.Single();
        return new CsrfState(token, CookiePair(GetSetCookie(response, "__Host-CSRF")));
    }

    private static async Task<HttpResponseMessage> SendJsonAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        object body,
        string host,
        CsrfState? csrf = null,
        string? additionalCookie = null)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Host = host;
        if (path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Add("X-Forwarded-For", $"auth-test-{Guid.NewGuid():N}");
        }

        if (csrf is not null)
        {
            request.Headers.Add("X-CSRF-Token", csrf.Token);
            var cookieHeader = additionalCookie is null
                ? csrf.Cookie
                : $"{csrf.Cookie}; {additionalCookie}";
            request.Headers.Add("Cookie", cookieHeader);
        }
        else if (additionalCookie is not null)
        {
            request.Headers.Add("Cookie", additionalCookie);
        }

        return await client.SendAsync(request);
    }

    private static string GetSetCookie(HttpResponseMessage response, string cookieName)
    {
        response.Headers.TryGetValues("Set-Cookie", out var values).Should().BeTrue();
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

    private sealed record SessionResponse(SessionUser User);

    private sealed record SessionUser(
        Guid Id,
        string Email,
        string DisplayName,
        IReadOnlyList<string> Roles,
        IReadOnlyList<SessionGroup> Groups);

    private sealed record SessionGroup(Guid Id, string Name);

    private sealed record InternalSessionValidationResponse(
        Guid UserId,
        string Role,
        IReadOnlyList<Guid> Groups,
        string AccessScopeHash,
        string Corpus);
}

public sealed class AuthWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid TestRoleId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public static readonly Guid TestGroupId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    public const string InternalServiceToken = "test-internal-service-token";
    public const string TestUserEmail = "viewer@example.com";
    private const string ValidPasswordHash =
        "pbkdf2-sha256$210000$AQIDBAUGBwgJCgsMDQ4PEA==$JdDMG3pUrVLfeEbOlhWCrHZ8tt8ULmvax1+L5RUnbdg=";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg16")
        .WithDatabase("advanced_rag_auth_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly string _jwtSigningKeysJson = CreateSigningKeysJson();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        await SeedUserAsync(db);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AppDatabase"] = _postgres.GetConnectionString(),
                ["Jwt:Issuer"] = "advanced-rag-dotnet-api",
                ["Jwt:Audience"] = "advanced-rag-chat",
                ["Jwt:SigningKeysJson"] = _jwtSigningKeysJson,
                ["Csrf:SigningKey"] = "local-test-csrf-signing-key-with-enough-entropy",
                ["InternalService:Token"] = InternalServiceToken,
            });
        });
    }

    private static async Task SeedUserAsync(AppDbContext db)
    {
        if (await db.Users.AnyAsync(user => user.Id == TestUserId))
        {
            return;
        }

        Role? existingViewerRole = await db.Roles.SingleOrDefaultAsync(role => role.Name == "Viewer");
        Role viewerRole = existingViewerRole ?? new Role { Id = TestRoleId, Name = "Viewer" };
        if (existingViewerRole is null)
        {
            db.Roles.Add(viewerRole);
        }

        db.Users.Add(new User
        {
            Id = TestUserId,
            Email = TestUserEmail,
            DisplayName = "Viewer User",
            PasswordHash = ValidPasswordHash,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        db.Groups.Add(new Group { Id = TestGroupId, Name = "Operations" });
        db.UserRoles.Add(new UserRole { UserId = TestUserId, RoleId = viewerRole.Id });
        db.UserGroups.Add(new UserGroup { UserId = TestUserId, GroupId = TestGroupId });
        await db.SaveChangesAsync();
    }

    private static string CreateSigningKeysJson()
    {
        using var rsa = RSA.Create(2048);
        var key = new[]
        {
            new
            {
                kid = "test-key",
                status = "current",
                @private = rsa.ExportPkcs8PrivateKeyPem(),
                @public = rsa.ExportSubjectPublicKeyInfoPem(),
            },
        };

        return JsonSerializer.Serialize(key);
    }
}
