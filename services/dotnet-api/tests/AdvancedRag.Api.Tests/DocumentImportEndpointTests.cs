using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AdvancedRag.App.Auth;
using AdvancedRag.App.Documents;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace AdvancedRag.Api.Tests;

public sealed class DocumentImportEndpointTests : IClassFixture<DocumentImportWebApplicationFactory>
{
    private const string DocxMime =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private readonly DocumentImportWebApplicationFactory _factory;

    public DocumentImportEndpointTests(DocumentImportWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ImportDocx_AsEditor_CreatesDraftAndReturnsDetail()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        LoginSession session = await LoginAsync(client, DocumentImportFakeAuthService.EditorEmail, "manage.localhost");

        using MultipartFormDataContent content = new();
        ByteArrayContent file = new([0x50, 0x4b, 0x03, 0x04]); // ZIP magic
        file.Headers.ContentType = new MediaTypeHeaderValue(DocxMime);
        content.Add(file, "file", "Quarterly Report.docx");

        using HttpRequestMessage request = new(HttpMethod.Post, "/api/documents/imports/docx") { Content = content };
        request.Headers.Host = "manage.localhost";
        request.Headers.Add("X-CSRF-Token", session.Csrf.Token);
        request.Headers.Add("Cookie", $"{session.Csrf.Cookie}; {session.SessionCookie}");

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        DocumentDetailResponse? body = await response.Content.ReadFromJsonAsync<DocumentDetailResponse>();
        body!.Title.Should().Be("Quarterly Report");
        _factory.Imports.LastCommand!.OriginalFilename.Should().Be("Quarterly Report.docx");
        _factory.Imports.LastCommand.ActorUserId.Should().Be(DocumentImportFakeAuthService.EditorUserId);
    }

    [Fact]
    public async Task ImportDocx_AsViewer_IsForbidden()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        LoginSession session = await LoginAsync(client, DocumentImportFakeAuthService.ViewerEmail, "manage.localhost");

        using MultipartFormDataContent content = new();
        ByteArrayContent file = new([0x50, 0x4b, 0x03, 0x04]);
        file.Headers.ContentType = new MediaTypeHeaderValue(DocxMime);
        content.Add(file, "file", "x.docx");

        using HttpRequestMessage request = new(HttpMethod.Post, "/api/documents/imports/docx") { Content = content };
        request.Headers.Host = "manage.localhost";
        request.Headers.Add("X-CSRF-Token", session.Csrf.Token);
        request.Headers.Add("Cookie", $"{session.Csrf.Cookie}; {session.SessionCookie}");

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<LoginSession> LoginAsync(HttpClient client, string email, string host)
    {
        CsrfState csrf = await GetCsrfAsync(client, host);
        HttpRequestMessage login = new(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email, password = DocumentImportFakeAuthService.ValidPassword }),
        };
        login.Headers.Host = host;
        login.Headers.Add("X-CSRF-Token", csrf.Token);
        login.Headers.Add("Cookie", csrf.Cookie);
        using HttpResponseMessage response = await client.SendAsync(login);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return new LoginSession(csrf, CookiePair(GetSetCookie(response, "__Host-session")));
    }

    private static async Task<CsrfState> GetCsrfAsync(HttpClient client, string host)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/csrf");
        request.Headers.Host = host;
        using HttpResponseMessage response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.TryGetValues("X-CSRF-Token", out IEnumerable<string>? tokens).Should().BeTrue();
        return new CsrfState(tokens!.Single(), CookiePair(GetSetCookie(response, "__Host-CSRF")));
    }

    private static string GetSetCookie(HttpResponseMessage response, string cookieName)
    {
        response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values).Should().BeTrue();
        return values!.Single(value => value.StartsWith(cookieName, StringComparison.Ordinal));
    }

    private static string CookiePair(string setCookie) => setCookie.Split(';', 2)[0];

    private sealed record CsrfState(string Token, string Cookie);

    private sealed record LoginSession(CsrfState Csrf, string SessionCookie);

    private sealed record DocumentDetailResponse(Guid Id, string Title, string State);
}

public sealed class DocumentImportWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly DocumentImportFakeAuthService _auth = new();
    public FakeDocumentImportService Imports { get; } = new();

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
                ["InternalService:Token"] = "test-internal-token",
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAuthService>();
            services.RemoveAll<IDocumentImportService>();
            services.AddSingleton<IAuthService>(_auth);
            services.AddSingleton<IDocumentImportService>(Imports);
        });
    }
}

public sealed class FakeDocumentImportService : IDocumentImportService
{
    public ImportDocxCommand? LastCommand { get; private set; }

    public Task<DocumentAggregate> ImportDocxAsync(ImportDocxCommand command, CancellationToken ct)
    {
        LastCommand = command;
        DocumentAggregate draft = DocumentAggregate.NewDraft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Path.GetFileNameWithoutExtension(command.OriginalFilename),
            string.Empty,
            string.Empty,
            "<p>imported</p>",
            Array.Empty<DocumentAccessRuleRecord>(),
            command.ActorUserId);
        return Task.FromResult(draft);
    }
}

public sealed class DocumentImportFakeAuthService : IAuthService
{
    public static readonly Guid EditorUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid ViewerUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public const string EditorEmail = "editor@example.com";
    public const string ViewerEmail = "viewer@example.com";
    public const string ValidPassword = "password";

    private readonly Dictionary<Guid, AuthenticatedUser> _users = new()
    {
        [EditorUserId] = new(EditorUserId, EditorEmail, "Editor User", ["DocumentEditor"], []),
        [ViewerUserId] = new(ViewerUserId, ViewerEmail, "Viewer User", ["Viewer"], []),
    };

    public Task<AuthenticatedUser?> AuthenticateAsync(string email, string password, CancellationToken ct)
    {
        AuthenticatedUser? user = _users.Values.SingleOrDefault(item =>
            item.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(password == ValidPassword ? user : null);
    }

    public Task<AuthenticatedUser?> GetActiveUserAsync(Guid userId, CancellationToken ct) =>
        Task.FromResult(_users.GetValueOrDefault(userId));
}
