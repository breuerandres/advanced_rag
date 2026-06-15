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
                organizationalUnitId = FakeUserAdministrationService.MarketingUnitId,
            },
            "manage.localhost",
            session.Csrf,
            session.SessionCookie);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        body.Should().NotBeNull();
        body!.Email.Should().Be("new.viewer@example.com");
        body.Roles.Should().Equal("Viewer");
        body.OrganizationalUnit.Should().NotBeNull();
        body.OrganizationalUnit!.Id.Should().Be(FakeUserAdministrationService.MarketingUnitId);
        body.MonthlyBudgetUsd.Should().Be(5m);
        body.CurrentSpendUsd.Should().Be(0m);
        body.RemainingBudgetUsd.Should().Be(5m);
    }

    [Fact]
    public async Task CreateUser_WithoutOrganizationalUnit_ReturnsValidationEnvelope()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var session = await LoginAsync(client, FakeAuthService.AdminEmail, "manage.localhost");

        using var response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/users",
            new
            {
                email = "missing.unit@example.com",
                displayName = "Missing Unit",
                password = "temporary-password",
                roles = new[] { "Viewer" },
                groupIds = Array.Empty<Guid>(),
            },
            "manage.localhost",
            session.Csrf,
            session.SessionCookie);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        body!.Error.Code.Should().Be("VALIDATION_FAILED");
        body.Error.Details.Should().ContainKey("field").WhoseValue.ToString().Should().Be("organizationalUnitId");
    }

    [Theory]
    [InlineData("Viewer")]
    [InlineData("DocumentEditor")]
    [InlineData("DocumentPublisher")]
    [InlineData("Admin")]
    public async Task SetUserRoles_AcceptsActiveHierarchicalRoles(string role)
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var session = await LoginAsync(client, FakeAuthService.AdminEmail, "manage.localhost");

        using var response = await SendJsonAsync(
            client,
            HttpMethod.Put,
            $"/api/users/{FakeAuthService.TargetUserId}/roles",
            new { roles = new[] { role } },
            "manage.localhost",
            session.Csrf,
            session.SessionCookie);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        body!.Roles.Should().Equal(role);
    }

    [Fact]
    public async Task SetUserGroups_IncrementsAccessScopeVersionInReturnedHash()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var session = await LoginAsync(client, FakeAuthService.AdminEmail, "manage.localhost");

        using var response = await SendJsonAsync(
            client,
            HttpMethod.Put,
            $"/api/users/{FakeAuthService.TargetUserId}/groups",
            new { groupIds = new[] { FakeUserAdministrationService.OperationsGroupId } },
            "manage.localhost",
            session.Csrf,
            session.SessionCookie);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        body!.AccessScopeHash.Should().Be("scope-hash-v2");
    }

    [Fact]
    public async Task SetUserOrganizationalUnit_AsAdmin_ReturnsUpdatedUnit()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var session = await LoginAsync(client, FakeAuthService.AdminEmail, "manage.localhost");

        var unitId = Guid.Parse("01000000-0000-0000-0000-000000000009");
        using var response = await SendJsonAsync(
            client,
            HttpMethod.Put,
            $"/api/users/{FakeAuthService.TargetUserId}/organizational-unit",
            new { organizationalUnitId = unitId },
            "manage.localhost",
            session.Csrf,
            session.SessionCookie);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        body!.OrganizationalUnit.Should().NotBeNull();
        body.OrganizationalUnit!.Id.Should().Be(unitId);
    }

    [Fact]
    public async Task SetUserRoles_AsDocumentPublisher_ReturnsForbidden()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var session = await LoginAsync(client, FakeAuthService.DocumentPublisherEmail, "manage.localhost");

        using HttpResponseMessage setRoles = await SendJsonAsync(
            client,
            HttpMethod.Put,
            $"/api/users/{FakeAuthService.DocumentPublisherUserId}/roles",
            new { roles = new[] { "Admin" } },
            "manage.localhost",
            session.Csrf,
            session.SessionCookie);

        setRoles.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListUsers_AsDocumentPublisher_ReturnsUsersAndBalances()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var session = await LoginAsync(client, FakeAuthService.DocumentPublisherEmail, "manage.localhost");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/users");
        request.Headers.Host = "manage.localhost";
        request.Headers.Add("Cookie", session.SessionCookie);

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserResponse[]>();
        body.Should().NotBeNull();
        body!.Should().ContainSingle(user =>
            user.Email == FakeAuthService.TargetEmail
            && user.MonthlyBudgetUsd == 5m
            && user.RemainingBudgetUsd == 5m);
    }

    [Fact]
    public async Task MutateAdminOnlyUserFields_AsDocumentPublisher_ReturnsForbidden()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var session = await LoginAsync(client, FakeAuthService.DocumentPublisherEmail, "manage.localhost");

        using HttpResponseMessage createUser = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/users",
            new
            {
                email = "blocked.viewer@example.com",
                displayName = "Blocked Viewer",
                password = "temporary-password",
                roles = new[] { "Viewer" },
                groupIds = Array.Empty<Guid>(),
            },
            "manage.localhost",
            session.Csrf,
            session.SessionCookie);
        using HttpResponseMessage setRoles = await SendJsonAsync(
            client,
            HttpMethod.Put,
            $"/api/users/{FakeAuthService.TargetUserId}/roles",
            new { roles = new[] { "Admin" } },
            "manage.localhost",
            session.Csrf,
            session.SessionCookie);
        using HttpResponseMessage setStatus = await SendJsonAsync(
            client,
            HttpMethod.Patch,
            $"/api/users/{FakeAuthService.TargetUserId}/status",
            new { isActive = false },
            "manage.localhost",
            session.Csrf,
            session.SessionCookie);
        using HttpResponseMessage setBudget = await SendJsonAsync(
            client,
            HttpMethod.Put,
            $"/api/users/{FakeAuthService.TargetUserId}/ai-budget",
            new { monthlyBudgetUsd = 1m, isDisabled = false },
            "manage.localhost",
            session.Csrf,
            session.SessionCookie);

        createUser.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        setRoles.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        setStatus.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        setBudget.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ManageGroupsAndAssignments_AsDocumentPublisher_ReturnsSuccess()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var session = await LoginAsync(client, FakeAuthService.DocumentPublisherEmail, "manage.localhost");

        using HttpResponseMessage createGroup = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/groups",
            new { name = "People Ops" },
            "manage.localhost",
            session.Csrf,
            session.SessionCookie);
        using HttpResponseMessage updateGroup = await SendJsonAsync(
            client,
            HttpMethod.Put,
            $"/api/groups/{FakeUserAdministrationService.OperationsGroupId}",
            new { name = "Operations Updated" },
            "manage.localhost",
            session.Csrf,
            session.SessionCookie);
        using HttpResponseMessage assignGroups = await SendJsonAsync(
            client,
            HttpMethod.Put,
            $"/api/users/{FakeAuthService.TargetUserId}/groups",
            new { groupIds = new[] { FakeUserAdministrationService.OperationsGroupId } },
            "manage.localhost",
            session.Csrf,
            session.SessionCookie);

        createGroup.StatusCode.Should().Be(HttpStatusCode.Created);
        updateGroup.StatusCode.Should().Be(HttpStatusCode.OK);
        assignGroups.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListUsers_AsViewer_ReturnsForbidden()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var session = await LoginAsync(client, FakeAuthService.TargetEmail, "manage.localhost");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/users");
        request.Headers.Host = "manage.localhost";
        request.Headers.Add("Cookie", session.SessionCookie);

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
        return new LoginSession(csrf, CookiePair(GetSetCookie(response, "__Host-session")));
    }

    private static async Task<HttpResponseMessage> LoginResponseAsync(HttpClient client, string email, string host)
    {
        var csrf = await GetCsrfAsync(client, host);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email, password = FakeAuthService.ValidPassword }),
        };
        request.Headers.Host = host;
        request.Headers.Add("X-CSRF-Token", csrf.Token);
        request.Headers.Add("Cookie", csrf.Cookie);
        request.Headers.Add("X-Forwarded-For", $"192.0.2.{Interlocked.Increment(ref _loginIpCounter)}");
        return await client.SendAsync(request);
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

    private static int _loginIpCounter;

    private sealed record ApiErrorEnvelope(ApiErrorBody Error);

    private sealed record ApiErrorBody(string Code, string Message, Dictionary<string, object> Details, string RequestId);

    private sealed record UserResponse(
        Guid Id,
        string Email,
        string DisplayName,
        bool IsActive,
        IReadOnlyList<string> Roles,
        IReadOnlyList<GroupResponse> Groups,
        OrganizationalUnitResponse? OrganizationalUnit,
        string AccessScopeHash,
        decimal? MonthlyBudgetUsd,
        decimal CurrentSpendUsd,
        decimal? RemainingBudgetUsd,
        bool IsBudgetDisabled);

    private sealed record GroupResponse(
        Guid Id,
        string Name,
        OrganizationalUnitResponse? OwnerOrganizationalUnit,
        string PublishingPolicy);

    private sealed record OrganizationalUnitResponse(Guid Id, string Name, Guid? ParentId, int Depth, bool IsActive);
}

public sealed class UserAdministrationWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly FakeAuthService _auth = new();
    private readonly FakeUserAdministrationService _users;
    private readonly string _openAiApiKeyFile;
    private readonly string _internalServiceTokenFile;

    public UserAdministrationWebApplicationFactory()
    {
        _users = new FakeUserAdministrationService(_auth);
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
            services.RemoveAll<IUserAdministrationService>();
            services.RemoveAll<ITenantConfigService>();
            services.AddSingleton<IAuthService>(_auth);
            services.AddSingleton<IUserAdministrationService>(_users);
            services.AddScoped<ITenantConfigService, FakeTenantConfigService>();
        });
    }
}

public sealed class FakeTenantConfigService : ITenantConfigService
{
    private TenantConfig _config = ToConfig(TenantConfigDraft.CreateDefault() with
    {
        SupportedLocales = ["es-AR", "en-US", "pt-BR"],
        LlmModel = "gpt-4.1-nano",
    });

    public Task<TenantConfig> GetAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_config);
    }

    public Task<TenantConfig> UpdateAsync(TenantConfigDraft draft, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _config = ToConfig(draft);
        return Task.FromResult(_config);
    }

    private static TenantConfig ToConfig(TenantConfigDraft draft)
    {
        return new TenantConfig(
            Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            draft.BrandName,
            draft.BrandLogoUrl,
            draft.BrandFaviconUrl,
            draft.PrimaryColor,
            draft.DefaultLocale,
            draft.SupportedLocales,
            draft.LlmProvider,
            draft.LlmModel,
            draft.LlmBaseUrl,
            draft.EmbeddingProvider,
            draft.EmbeddingModel,
            draft.EmbeddingDimensions,
            draft.RerankerProvider,
            draft.RerankerModel,
            draft.RerankerBaseUrl,
            draft.EnableBm25,
            draft.EnableReranker,
            draft.EnableConversationalMemory,
            draft.EnableQueryRewrite,
            draft.RagTopKVector,
            draft.RagTopKBm25,
            draft.RagTopKFinal,
            draft.RrfK,
            draft.ConversationHistoryTurns,
            draft.CacheTtlHours,
            draft.CacheSimilarityThreshold,
            draft.DefaultMonthlyBudgetUsd,
            draft.GlobalDailyBudgetUsd,
            draft.EnableVlmImageDescription,
            draft.EnableOtel,
            draft.S3Endpoint,
            draft.S3Bucket,
            draft.S3Region,
            draft.CustomerTimezone,
            draft.ImportMaxFileSizeMb,
            draft.ChatMaxQuestionChars,
            draft.SeededFromEnv,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }
}

public sealed class FakeAuthService : IAuthService
{
    public static readonly Guid AdminUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid DocumentPublisherUserId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    public static readonly Guid DocumentManagerUserId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    public static readonly Guid TargetUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public const string AdminEmail = "admin@example.com";
    public const string DocumentPublisherEmail = "publisher@example.com";
    public const string DocumentManagerEmail = "manager@example.com";
    public const string TargetEmail = "target@example.com";
    public const string ValidPassword = "password";
    private readonly Dictionary<Guid, FakeUserState> _users = new()
    {
        [AdminUserId] = new(AdminUserId, AdminEmail, "Admin User", true, ["Admin"], []),
        [DocumentPublisherUserId] = new(
            DocumentPublisherUserId,
            DocumentPublisherEmail,
            "Document Publisher",
            true,
            ["DocumentPublisher"],
            []),
        [DocumentManagerUserId] = new(
            DocumentManagerUserId,
            DocumentManagerEmail,
            "Document Manager",
            true,
            ["DocumentManager"],
            []),
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
    public static readonly Guid RootUnitId = Guid.Parse("01000000-0000-0000-0000-000000000001");
    public static readonly Guid MarketingUnitId = Guid.Parse("01000000-0000-0000-0000-000000000003");
    private readonly FakeAuthService _auth;
    private readonly OrganizationalUnitRecord _marketing = new(MarketingUnitId, "Marketing", RootUnitId, 2, true);
    private readonly GroupRecord _operations;

    public FakeUserAdministrationService(FakeAuthService auth)
    {
        _auth = auth;
        _operations = new GroupRecord(OperationsGroupId, "Operations", null, "OwnerScope");
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

    public Task<GroupRecord> UpdateGroupAsync(UpdateGroupCommand command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_operations with { Name = command.Name });
    }

    public Task<UserManagementUser> CreateUserAsync(CreateUserCommand command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (command.OrganizationalUnitId is null)
        {
            throw new UserAdministrationException(
                "VALIDATION_FAILED",
                400,
                "Organizational unit is required.",
                new Dictionary<string, object?> { ["field"] = "organizationalUnitId" });
        }

        return Task.FromResult(
            new UserManagementUser(
                Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                command.Email,
                command.DisplayName,
                true,
                command.RoleNames,
                [ _operations ],
                _marketing,
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
        return Task.FromResult(CreateTargetUser(isActive: true, roles: command.RoleNames));
    }

    public Task<UserManagementUser> SetUserGroupsAsync(SetUserGroupsCommand command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(CreateTargetUser(isActive: true) with { AccessScopeHash = "scope-hash-v2" });
    }

    public Task<UserManagementUser> SetUserOrganizationalUnitAsync(
        SetUserOrganizationalUnitCommand command,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var unit = new OrganizationalUnitRecord(command.OrganizationalUnitId, "Reassigned", RootUnitId, 2, true);
        return Task.FromResult(CreateTargetUser(isActive: true) with { OrganizationalUnit = unit });
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

    private UserManagementUser CreateTargetUser(bool isActive, IReadOnlyList<string>? roles = null)
    {
        return new UserManagementUser(
            FakeAuthService.TargetUserId,
            FakeAuthService.TargetEmail,
            "Target User",
            isActive,
            roles ?? ["Viewer"],
            [],
            _marketing,
            "scope-hash",
            5m,
            0m,
            false);
    }
}
