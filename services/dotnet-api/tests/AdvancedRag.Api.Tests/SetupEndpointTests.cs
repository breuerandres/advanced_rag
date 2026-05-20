using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
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

public sealed class SetupEndpointTests : IClassFixture<SetupWebApplicationFactory>
{
    private const string FirstAdminEmail = "first.admin@example.com";
    private const string FirstAdminPassword = "Correct Horse Battery Staple 42!";
    private readonly SetupWebApplicationFactory _factory;

    public SetupEndpointTests(SetupWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FirstRunSetup_CreatesAdminThenBlocksFurtherSetupAndAllowsLogin()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        SetupStatusResponse initialStatus = await GetSetupStatusAsync(client);
        initialStatus.SetupRequired.Should().BeTrue();
        initialStatus.AdminExists.Should().BeFalse();
        initialStatus.RequiredRoles.Should().Equal("Admin", "DocumentManager", "Viewer");

        CsrfState csrf = await GetCsrfAsync(client);
        using HttpResponseMessage created = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/setup/admin",
            new
            {
                email = FirstAdminEmail,
                displayName = "First Admin",
                password = FirstAdminPassword,
            },
            csrf);

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        SetupAdminResponse createdBody = (await created.Content.ReadFromJsonAsync<SetupAdminResponse>())!;
        createdBody.User.Email.Should().Be(FirstAdminEmail);
        createdBody.User.Roles.Should().Equal("Admin");

        await AssertAdminBootstrapRowsAsync(createdBody.User.Id);

        using HttpResponseMessage login = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/auth/login",
            new { email = FirstAdminEmail, password = FirstAdminPassword },
            csrf);
        login.StatusCode.Should().Be(HttpStatusCode.OK);

        SetupStatusResponse completedStatus = await GetSetupStatusAsync(client);
        completedStatus.SetupRequired.Should().BeFalse();
        completedStatus.AdminExists.Should().BeTrue();

        using HttpResponseMessage blocked = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/setup/admin",
            new
            {
                email = "second.admin@example.com",
                displayName = "Second Admin",
                password = FirstAdminPassword,
            },
            csrf);

        blocked.StatusCode.Should().Be(HttpStatusCode.Conflict);
        ApiErrorEnvelope error = (await blocked.Content.ReadFromJsonAsync<ApiErrorEnvelope>())!;
        error.Error.Code.Should().Be("SETUP_ALREADY_COMPLETED");
        error.Error.RequestId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateFirstAdmin_WithMissingEmail_ReturnsValidationEnvelope()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        CsrfState csrf = await GetCsrfAsync(client);

        using HttpResponseMessage response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/setup/admin",
            new
            {
                email = " ",
                displayName = "First Admin",
                password = FirstAdminPassword,
            },
            csrf);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ApiErrorEnvelope error = (await response.Content.ReadFromJsonAsync<ApiErrorEnvelope>())!;
        error.Error.Code.Should().Be("VALIDATION_FAILED");
        error.Error.Details.Should().ContainKey("field");
    }

    private async Task AssertAdminBootstrapRowsAsync(Guid adminUserId)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        (await db.Roles.AsNoTracking().Select(role => role.Name).OrderBy(name => name).ToListAsync())
            .Should()
            .Equal("Admin", "DocumentManager", "Viewer");
        (await db.UserAiBudgetLimits.AsNoTracking().SingleAsync(budget => budget.UserId == adminUserId))
            .MonthlyBudgetUsd.Should().Be(5m);
    }

    private static async Task<SetupStatusResponse> GetSetupStatusAsync(HttpClient client)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/setup/status");
        request.Headers.Host = "manage.localhost";

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<SetupStatusResponse>())!;
    }

    private static async Task<CsrfState> GetCsrfAsync(HttpClient client)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/csrf");
        request.Headers.Host = "manage.localhost";

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
        CsrfState csrf)
    {
        HttpRequestMessage request = new(method, path)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Host = "manage.localhost";
        request.Headers.Add("X-CSRF-Token", csrf.Token);
        request.Headers.Add("Cookie", csrf.Cookie);
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

    private sealed record SetupStatusResponse(
        bool SetupRequired,
        bool AdminExists,
        bool DatabaseReady,
        IReadOnlyList<string> RequiredRoles);

    private sealed record SetupAdminResponse(SetupUserResponse User);

    private sealed record SetupUserResponse(
        Guid Id,
        string Email,
        string DisplayName,
        IReadOnlyList<string> Roles);

    private sealed record ApiErrorEnvelope(ApiErrorBody Error);

    private sealed record ApiErrorBody(string Code, string Message, Dictionary<string, object> Details, string RequestId);
}

public sealed class SetupWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg16")
        .WithDatabase("advanced_rag_setup_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly string _jwtSigningKeysJson = CreateSigningKeysJson();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
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
            });
        });
    }

    private static string CreateSigningKeysJson()
    {
        using RSA rsa = RSA.Create(2048);
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
