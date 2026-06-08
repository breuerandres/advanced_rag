using System.Net;
using System.Net.Http.Json;
using AdvancedRag.App.Auth;
using AdvancedRag.App.Configuration;
using AdvancedRag.App.Documents;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace AdvancedRag.Api.Tests;

public sealed class DocumentEndpointTests : IClassFixture<DocumentEndpointWebApplicationFactory>
{
    private readonly DocumentEndpointWebApplicationFactory _factory;

    public DocumentEndpointTests(DocumentEndpointWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateDocument_WithAccessRules_MapsRulesToLifecycleCommand()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        LoginSession session = await LoginAsync(client, FakeDocumentAuthService.AdminEmail, "manage.localhost");

        using HttpResponseMessage response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/documents",
            new
            {
                title = "Protocolo de comunicacion en crisis",
                documentType = "Procedimiento",
                audience = "Comunicacion",
                contentHtml = "<p>Contenido</p>",
                accessRules = new[]
                {
                    new
                    {
                        organizationalUnitId = FakeDocumentLifecycleService.ComunicacionId,
                        groupIds = new[] { FakeDocumentLifecycleService.ComiteCrisisId },
                    },
                },
            },
            "manage.localhost",
            session.Csrf,
            session.SessionCookie);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        DocumentDetailResponse? body = await response.Content.ReadFromJsonAsync<DocumentDetailResponse>();
        body.Should().NotBeNull();
        body!.AccessRules.Should().ContainSingle(rule =>
            rule.OrganizationalUnitId == FakeDocumentLifecycleService.ComunicacionId
            && rule.GroupIds.Contains(FakeDocumentLifecycleService.ComiteCrisisId));
        _factory.Documents.LastCreateCommand.Should().NotBeNull();
        _factory.Documents.LastCreateCommand!.AccessRules.Should().ContainSingle(rule =>
            rule.OrganizationalUnitId == FakeDocumentLifecycleService.ComunicacionId
            && rule.GroupIds.Contains(FakeDocumentLifecycleService.ComiteCrisisId));
    }

    [Fact]
    public async Task CreateDocument_WithEmptyAccessRule_ReturnsValidationEnvelope()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        LoginSession session = await LoginAsync(client, FakeDocumentAuthService.AdminEmail, "manage.localhost");

        using HttpResponseMessage response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/documents",
            new
            {
                title = "Documento sin regla",
                documentType = "Politica",
                audience = "Todos",
                contentHtml = "<p>Contenido</p>",
                accessRules = new[] { new { organizationalUnitId = (Guid?)null, groupIds = Array.Empty<Guid>() } },
            },
            "manage.localhost",
            session.Csrf,
            session.SessionCookie);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ApiErrorEnvelope? body = await response.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        body!.Error.Code.Should().Be("VALIDATION_FAILED");
    }

    [Fact]
    public async Task CreateDocument_AsComunicacionPublisher_RejectsGerentesWithoutGrant()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        LoginSession session = await LoginAsync(client, FakeDocumentAuthService.PublisherEmail, "manage.localhost");

        using HttpResponseMessage response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/documents",
            new
            {
                title = "Documento Gerentes",
                documentType = "Politica",
                audience = "Gerentes",
                contentHtml = "<p>Contenido</p>",
                accessRules = new[]
                {
                    new
                    {
                        organizationalUnitId = FakeDocumentLifecycleService.ComunicacionId,
                        groupIds = new[] { FakeDocumentLifecycleService.GerentesId },
                    },
                },
            },
            "manage.localhost",
            session.Csrf,
            session.SessionCookie);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        ApiErrorEnvelope? body = await response.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        body!.Error.Code.Should().Be("AUTH_FORBIDDEN");
    }

    [Fact]
    public async Task CreateDocument_AsComunicacionPublisher_AllowsOwnedCrisisGroup()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        LoginSession session = await LoginAsync(client, FakeDocumentAuthService.PublisherEmail, "manage.localhost");

        using HttpResponseMessage response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/documents",
            new
            {
                title = "Protocolo crisis",
                documentType = "Procedimiento",
                audience = "Comunicacion",
                contentHtml = "<p>Contenido</p>",
                accessRules = new[]
                {
                    new
                    {
                        organizationalUnitId = FakeDocumentLifecycleService.ComunicacionId,
                        groupIds = new[] { FakeDocumentLifecycleService.ComiteCrisisId },
                    },
                },
            },
            "manage.localhost",
            session.Csrf,
            session.SessionCookie);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData("01000000-0000-0000-0000-000000000002", HttpStatusCode.OK)]
    [InlineData("01000000-0000-0000-0000-000000000003", HttpStatusCode.OK)]
    [InlineData("01000000-0000-0000-0000-000000000004", HttpStatusCode.OK)]
    [InlineData("01000000-0000-0000-0000-000000000006", HttpStatusCode.Forbidden)]
    public async Task RequestPublish_AsComunicacionPublisher_IsScopedToComunicacionBranch(
        string documentUnitId,
        HttpStatusCode expectedStatus)
    {
        _factory.Documents.ConfigurePublishDocument(Guid.Parse(documentUnitId));
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        LoginSession session = await LoginAsync(client, FakeDocumentAuthService.PublisherEmail, "manage.localhost");

        using HttpResponseMessage response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            $"/api/documents/{FakeDocumentLifecycleService.DocumentId}/request-publish",
            new { },
            "manage.localhost",
            session.Csrf,
            session.SessionCookie);

        response.StatusCode.Should().Be(expectedStatus);
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
            Content = JsonContent.Create(new { email, password = FakeDocumentAuthService.ValidPassword }),
        };
        request.Headers.Host = host;
        request.Headers.Add("X-CSRF-Token", csrf.Token);
        request.Headers.Add("Cookie", csrf.Cookie);
        request.Headers.Add("X-Forwarded-For", $"203.0.113.{Interlocked.Increment(ref _loginIpCounter)}");
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

    private sealed record DocumentDetailResponse(
        Guid Id,
        string Title,
        string State,
        IReadOnlyList<DocumentAccessRuleResponse> AccessRules);

    private sealed record DocumentAccessRuleResponse(
        Guid Id,
        Guid? OrganizationalUnitId,
        IReadOnlyList<Guid> GroupIds);

    private sealed record ApiErrorEnvelope(ApiErrorBody Error);

    private sealed record ApiErrorBody(string Code, string Message, Dictionary<string, object> Details, string RequestId);
}

public sealed class DocumentEndpointWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly FakeDocumentAuthService _auth = new();
    private readonly string _openAiApiKeyFile;
    private readonly string _internalServiceTokenFile;

    public DocumentEndpointWebApplicationFactory()
    {
        Documents = new FakeDocumentLifecycleService();
        _openAiApiKeyFile = Path.GetTempFileName();
        _internalServiceTokenFile = Path.GetTempFileName();
        File.WriteAllText(_openAiApiKeyFile, "configured-openai-key-placeholder");
        File.WriteAllText(_internalServiceTokenFile, "configured-internal-service-token-placeholder");
    }

    public FakeDocumentLifecycleService Documents { get; }

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
            services.RemoveAll<IDocumentLifecycleService>();
            services.RemoveAll<IDocumentImportExtractionService>();
            services.RemoveAll<ITenantConfigService>();
            services.AddSingleton<IAuthService>(_auth);
            services.AddSingleton<IDocumentLifecycleService>(Documents);
            services.AddSingleton<IDocumentImportExtractionService, FakeDocumentImportExtractionService>();
            services.AddSingleton<ITenantConfigService, FakeTenantConfigService>();
        });
    }
}

public sealed class FakeDocumentAuthService : IAuthService
{
    public static readonly Guid AdminUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid PublisherUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public const string AdminEmail = "admin@example.com";
    public const string PublisherEmail = "publisher@example.com";
    public const string ValidPassword = "password";

    public Task<AuthenticatedUser?> AuthenticateAsync(string email, string password, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        AuthenticatedUser? user = email.ToLowerInvariant() switch
        {
            AdminEmail when password == ValidPassword => new AuthenticatedUser(AdminUserId, AdminEmail, "Admin User", ["Admin"], []),
            PublisherEmail when password == ValidPassword => new AuthenticatedUser(PublisherUserId, PublisherEmail, "Publisher User", ["DocumentPublisher"], []),
            _ => null,
        };
        return Task.FromResult(user);
    }

    public Task<AuthenticatedUser?> GetActiveUserAsync(Guid userId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        AuthenticatedUser? user = userId == AdminUserId
            ? new AuthenticatedUser(AdminUserId, AdminEmail, "Admin User", ["Admin"], [])
            : userId == PublisherUserId
                ? new AuthenticatedUser(PublisherUserId, PublisherEmail, "Publisher User", ["DocumentPublisher"], [])
                : null;
        return Task.FromResult(user);
    }
}

public sealed class FakeDocumentLifecycleService : IDocumentLifecycleService
{
    public static readonly Guid DocumentId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid VersionId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static readonly Guid ComunicacionId = Guid.Parse("01000000-0000-0000-0000-000000000002");
    public static readonly Guid MarketingId = Guid.Parse("01000000-0000-0000-0000-000000000003");
    public static readonly Guid ProduccionId = Guid.Parse("01000000-0000-0000-0000-000000000004");
    public static readonly Guid SistemasId = Guid.Parse("01000000-0000-0000-0000-000000000006");
    public static readonly Guid ComiteCrisisId = Guid.Parse("02000000-0000-0000-0000-000000000002");
    public static readonly Guid GerentesId = Guid.Parse("02000000-0000-0000-0000-000000000001");

    private Guid _publishDocumentUnitId = ComunicacionId;

    public CreateDocumentCommand? LastCreateCommand { get; private set; }

    public void ConfigurePublishDocument(Guid organizationalUnitId)
    {
        _publishDocumentUnitId = organizationalUnitId;
    }

    public Task<IReadOnlyList<DocumentSummary>> ListAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<DocumentSummary>>([]);
    }

    public Task<DocumentAggregate?> GetAsync(Guid documentId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<DocumentAggregate?>(CreateDocument([new DocumentAccessRuleRecord(Guid.NewGuid(), ComunicacionId, [])]));
    }

    public Task<DocumentAggregate> CreateDraftAsync(CreateDocumentCommand command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        LastCreateCommand = command;
        ValidateRules(command.AccessRules);
        return Task.FromResult(CreateDocument(ToRecords(command.AccessRules)));
    }

    public Task<DocumentAggregate> UpdateDraftAsync(UpdateDraftCommand command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ValidateRules(command.AccessRules);
        return Task.FromResult(CreateDocument(ToRecords(command.AccessRules)));
    }

    public Task<DocumentAggregate> SendToReviewAsync(SendToReviewCommand command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(CreateDocument([new DocumentAccessRuleRecord(Guid.NewGuid(), ComunicacionId, [])]));
    }

    public Task<DocumentAggregate> ReturnToDraftAsync(ReturnToDraftCommand command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(CreateDocument([new DocumentAccessRuleRecord(Guid.NewGuid(), ComunicacionId, [])]));
    }

    public Task<DocumentAggregate> RequestPublishAsync(RequestPublishCommand command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_publishDocumentUnitId == SistemasId)
        {
            throw new DocumentLifecycleException("AUTH_FORBIDDEN", 403, "Actor cannot publish this document.");
        }

        return Task.FromResult(CreateDocument([new DocumentAccessRuleRecord(Guid.NewGuid(), _publishDocumentUnitId, [])]));
    }

    public Task<DocumentAggregate> ArchiveAsync(ArchiveDocumentCommand command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(CreateDocument([new DocumentAccessRuleRecord(Guid.NewGuid(), ComunicacionId, [])]));
    }

    public Task<DocumentAggregate> RestoreAsync(RestoreDocumentCommand command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(CreateDocument([new DocumentAccessRuleRecord(Guid.NewGuid(), ComunicacionId, [])]));
    }

    private static void ValidateRules(IReadOnlyList<DocumentAccessRuleDraft> rules)
    {
        if (rules.Count == 0 || rules.Any(rule => rule.OrganizationalUnitId is null && rule.GroupIds.Count == 0))
        {
            throw new DocumentLifecycleException(
                "VALIDATION_FAILED",
                400,
                "At least one document access rule is required.",
                new Dictionary<string, object?> { ["field"] = "accessRules" });
        }

        if (rules.Any(rule => rule.GroupIds.Contains(GerentesId)))
        {
            throw new DocumentLifecycleException(
                "AUTH_FORBIDDEN",
                403,
                "Actor cannot use this group for publishing.");
        }
    }

    private static IReadOnlyList<DocumentAccessRuleRecord> ToRecords(IReadOnlyList<DocumentAccessRuleDraft> rules)
    {
        return rules
            .Select(rule => new DocumentAccessRuleRecord(Guid.NewGuid(), rule.OrganizationalUnitId, rule.GroupIds))
            .ToArray();
    }

    private static DocumentAggregate CreateDocument(IReadOnlyList<DocumentAccessRuleRecord> rules)
    {
        return DocumentAggregate.NewDraft(
            DocumentId,
            VersionId,
            "Protocolo de comunicacion en crisis",
            "Procedimiento",
            "Comunicacion",
            "<p>Contenido</p>",
            rules,
            FakeDocumentAuthService.AdminUserId);
    }
}

public sealed class FakeDocumentImportExtractionService : IDocumentImportExtractionService
{
    public Task<ImportExtractionResult> ExtractAsync(ImportExtractionCommand command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        throw new NotSupportedException();
    }
}
