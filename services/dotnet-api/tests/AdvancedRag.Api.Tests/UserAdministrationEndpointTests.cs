using System.Net;
using System.Net.Http.Json;
using AdvancedRag.App.Auth;
using AdvancedRag.App.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace AdvancedRag.Api.Tests;

public sealed class UserAdministrationEndpointTests
    : IClassFixture<UserAdministrationWebApplicationFactory>
{
    private readonly UserAdministrationWebApplicationFactory _factory;

    public UserAdministrationEndpointTests(UserAdministrationWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateUser_AsAdmin_ReturnsCreatedUserWithDefaultAiBudget()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var session = await LoginAsync(client, FakeAuthService.AdminEmail, "manage.localhost");

        using var response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/users",
            new
            {
                email = "new.viewer@example.com",
                displayName = "New Viewer",
                password = "temporary-password",
                roles = new[] { "Viewer" },
                groupIds = new[] { FakeUserAdministrationService.OperationsGroupId },
            },
            "manage.localhost",
            session.Csrf,
            session.SessionCookie);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        body.Should().NotBeNull();
        body!.Email.Should().Be("new.viewer@example.com");
        body.Roles.Should().Equal("Viewer");
        body.MonthlyBudgetUsd.Should().Be(5m);
        body.CurrentSpendUsd.Should().Be(0m);
        body.RemainingBudgetUsd.Should().Be(5m);
    }

    [Fact]
    public async Task SetUserStatus_DeactivatesUserAndBlocksNewLogin()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var initialTargetLogin = await LoginResponseAsync(client, FakeAuthService.TargetEmail, "manage.localhost");
        initialTargetLogin.StatusCode.Should().Be(HttpStatusCode.OK);
        initialTargetLogin.Dispose();

        var adminSession = await LoginAsync(client, FakeAuthService.AdminEmail, "manage.localhost");
        using var response = await SendJsonAsync(
            client,
            HttpMethod.Patch,
            $"/api/users/{FakeAuthService.TargetUserId}/status",
            new { isActive = false },
            "manage.localhost",
            adminSession.Csrf,
            adminSession.SessionCookie);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var targetLoginAfterDeactivation = await LoginResponseAsync(
            client,
            FakeAuthService.TargetEmail,
            "manage.localhost");
        targetLoginAfterDeactivation.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SetAiBudget_WithInvalidValue_ReturnsValidationEnvelope()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var session = await LoginAsync(client, FakeAuthService.AdminEmail, "manage.localhost");

        using var response = await SendJsonAsync(
            client,
            HttpMethod.Put,
            $"/api/users/{FakeAuthService.TargetUserId}/ai-budget",
            new { monthlyBudgetUsd = -1m, isDisabled = false },
            "manage.localhost",
            session.Csrf,
            session.SessionCookie);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        body!.Error.Code.Should().Be("VALIDATION_FAILED");
        body.Error.RequestId.Should().NotBeNullOrWhiteSpace();
    }

    private static async Task<LoginSession> LoginAsync(HttpClient client, string email, string host)
    {
        var response = await LoginResponseAsync(client, email, host);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var csrf = await GetCsrfAsync(client, host);
        return new LoginSession(csrf, CookiePair(GetSetCookie(response, "__Host-advanced-rag-session")));
    }

    private static async Task<HttpResponseMessage> LoginResponseAsync(HttpClient client, string email, string host)
    {
        var csrf = await GetCsrfAsync(client, host);
        return await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/auth/login",
            new { email, password = FakeAuthService.ValidPassword },
            host,
            csrf);
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
        CsrfState csrf,
        string? additionalCookie = null)
    {
        var request = new HttpRequestMessage(method, path)
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
        response.Headers.TryGetValues("Set-Cookie", out var values).Should().BeTrue();
        return values!.Single(value => value.StartsWith(cookieName, StringComparison.Ordinal));
    }

    private static string CookiePair(string setCookie) => setCookie.Split(';', 2)[0];

    private sealed record CsrfState(string Token, string Cookie);

    private sealed record LoginSession(CsrfState Csrf, string SessionCookie);

    private sealed record ApiErrorEnvelope(ApiErrorBody Error);

    private sealed record ApiErrorBody(string Code, string Message, Dictionary<string, object> Details, string RequestId);

    private sealed record UserResponse(
        Guid Id,
        string Email,
        string DisplayName,
        bool IsActive,
        IReadOnlyList<string> Roles,
        IReadOnlyList<GroupResponse> Groups,
        string AccessScopeHash,
        decimal? MonthlyBudgetUsd,
        decimal CurrentSpendUsd,
        decimal? RemainingBudgetUsd,
        bool IsBudgetDisabled);

    private sealed record GroupResponse(Guid Id, string Name);
}

public sealed class UserAdministrationWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly FakeAuthService _auth = new();
    private readonly FakeUserAdministrationService _users;

    public UserAdministrationWebApplicationFactory()
    {
        _users = new FakeUserAdministrationService(_auth);
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
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAuthService>();
            services.RemoveAll<IUserAdministrationService>();
            services.AddSingleton<IAuthService>(_auth);
            services.AddSingleton<IUserAdministrationService>(_users);
        });
    }
}

public sealed class FakeAuthService : IAuthService
{
    public static readonly Guid AdminUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid TargetUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public const string AdminEmail = "admin@example.com";
    public const string TargetEmail = "target@example.com";
    public const string ValidPassword = "password";
    private readonly Dictionary<Guid, FakeUserState> _users = new()
    {
        [AdminUserId] = new(AdminUserId, AdminEmail, "Admin User", true, ["Admin"], []),
        [TargetUserId] = new(TargetUserId, TargetEmail, "Target User", true, ["Viewer"], []),
    };

    public Task<AuthenticatedUser?> AuthenticateAsync(string email, string password, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var user = _users.Values.SingleOrDefault(item =>
            item.IsActive && item.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(password == ValidPassword ? user?.ToAuthenticatedUser() : null);
    }

    public Task<AuthenticatedUser?> GetActiveUserAsync(Guid userId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(
            _users.TryGetValue(userId, out var user) && user.IsActive
                ? user.ToAuthenticatedUser()
                : null);
    }

    public void SetActive(Guid userId, bool isActive)
    {
        var user = _users[userId];
        _users[userId] = user with { IsActive = isActive };
    }

    private sealed record FakeUserState(
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

public sealed class FakeUserAdministrationService : IUserAdministrationService
{
    public static readonly Guid OperationsGroupId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private readonly FakeAuthService _auth;
    private readonly GroupRecord _operations = new(OperationsGroupId, "Operations");

    public FakeUserAdministrationService(FakeAuthService auth)
    {
        _auth = auth;
    }

    public Task<IReadOnlyList<UserManagementUser>> ListUsersAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<UserManagementUser>>([CreateTargetUser(isActive: true)]);
    }

    public Task<IReadOnlyList<GroupRecord>> ListGroupsAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<GroupRecord>>([_operations]);
    }

    public Task<GroupRecord> CreateGroupAsync(CreateGroupCommand command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_operations with { Name = command.Name });
    }

    public Task<UserManagementUser> CreateUserAsync(CreateUserCommand command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(
            new UserManagementUser(
                Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                command.Email,
                command.DisplayName,
                true,
                command.RoleNames,
                [ _operations ],
                "scope-hash",
                5m,
                0m,
                false));
    }

    public Task<UserManagementUser?> GetUserAsync(Guid userId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<UserManagementUser?>(CreateTargetUser(isActive: true));
    }

    public Task<UserManagementUser> SetUserRolesAsync(SetUserRolesCommand command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(CreateTargetUser(isActive: true));
    }

    public Task<UserManagementUser> SetUserGroupsAsync(SetUserGroupsCommand command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(CreateTargetUser(isActive: true));
    }

    public Task<UserManagementUser> SetUserActiveStatusAsync(SetUserActiveStatusCommand command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _auth.SetActive(command.UserId, command.IsActive);
        return Task.FromResult(CreateTargetUser(command.IsActive));
    }

    public Task<UserManagementUser> SetUserAiBudgetAsync(SetUserAiBudgetCommand command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (command.MonthlyBudgetUsd < 0)
        {
            throw new UserAdministrationException(
                "VALIDATION_FAILED",
                400,
                "Monthly budget must be non-negative.");
        }

        return Task.FromResult(CreateTargetUser(isActive: true));
    }

    private UserManagementUser CreateTargetUser(bool isActive)
    {
        return new UserManagementUser(
            FakeAuthService.TargetUserId,
            FakeAuthService.TargetEmail,
            "Target User",
            isActive,
            ["Viewer"],
            [],
            "scope-hash",
            5m,
            0m,
            false);
    }
}
