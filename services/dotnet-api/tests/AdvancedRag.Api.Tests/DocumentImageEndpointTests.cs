using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using AdvancedRag.App.Auth;
using AdvancedRag.App.DocumentImages;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace AdvancedRag.Api.Tests;

public sealed class DocumentImageEndpointTests : IClassFixture<DocumentImageWebApplicationFactory>
{
    private static readonly Guid DocumentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ImageId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly DocumentImageWebApplicationFactory _factory;

    public DocumentImageEndpointTests(DocumentImageWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UploadImage_ReturnsStableAppUrl()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        LoginSession session = await LoginAsync(client, DocumentImageFakeAuthService.EditorEmail, "manage.localhost");
        using MultipartFormDataContent content = new();
        ByteArrayContent file = new([0x89, 0x50, 0x4e, 0x47]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, "file", "diagram.png");
        content.Add(new StringContent("Architecture diagram"), "altText");

        using HttpRequestMessage request = new(HttpMethod.Post, $"/api/documents/{DocumentId}/images")
        {
            Content = content,
        };
        request.Headers.Host = "manage.localhost";
        request.Headers.Add("X-CSRF-Token", session.Csrf.Token);
        request.Headers.Add("Cookie", $"{session.Csrf.Cookie}; {session.SessionCookie}");

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        DocumentImageUploadResponse? body = await response.Content.ReadFromJsonAsync<DocumentImageUploadResponse>();
        body.Should().BeEquivalentTo(new
        {
            ImageId,
            Url = $"/api/document-images/{ImageId}/content",
            AltText = "Architecture diagram",
        });
        _factory.Images.LastUploadCommand.Should().BeEquivalentTo(new
        {
            DocumentId,
            FileName = "diagram.png",
            ContentType = "image/png",
            SizeBytes = 4L,
            AltText = "Architecture diagram",
            ActorUserId = DocumentImageFakeAuthService.EditorUserId,
        });
    }

    [Fact]
    public async Task GetImageContent_WithSession_ReturnsStoredBytes()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        LoginSession session = await LoginAsync(client, DocumentImageFakeAuthService.ViewerEmail, "docs.localhost");
        using HttpRequestMessage request = new(HttpMethod.Get, $"/api/document-images/{ImageId}/content");
        request.Headers.Host = "docs.localhost";
        request.Headers.Add("Cookie", session.SessionCookie);

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().Equal([0x89, 0x50, 0x4e, 0x47]);
        _factory.Images.LastGetCommand.Should().BeEquivalentTo(new
        {
            ImageId,
            UserId = DocumentImageFakeAuthService.ViewerUserId,
            Roles = new[] { "Viewer" },
        });
    }

    private static async Task<LoginSession> LoginAsync(HttpClient client, string email, string host)
    {
        CsrfState csrf = await GetCsrfAsync(client, host);
        using HttpResponseMessage response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/auth/login",
            new { email, password = DocumentImageFakeAuthService.ValidPassword },
            host,
            csrf);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return new LoginSession(csrf, CookiePair(GetSetCookie(response, "__Host-session")));
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

    private sealed record LoginSession(CsrfState Csrf, string SessionCookie);

    private sealed record DocumentImageUploadResponse(Guid ImageId, string Url, string AltText);
}

public sealed class DocumentImageWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly DocumentImageFakeAuthService _auth = new();
    public FakeDocumentImageService Images { get; } = new();

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
            services.RemoveAll<IDocumentImageService>();
            services.AddSingleton<IAuthService>(_auth);
            services.AddSingleton<IDocumentImageService>(Images);
        });
    }
}

public sealed class FakeDocumentImageService : IDocumentImageService
{
    public UploadDocumentImageCommand? LastUploadCommand { get; private set; }
    public GetDocumentImageContentCommand? LastGetCommand { get; private set; }

    public Task<DocumentImageUploadResult> UploadAsync(UploadDocumentImageCommand command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        LastUploadCommand = command;
        return Task.FromResult(new DocumentImageUploadResult(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "/api/document-images/22222222-2222-2222-2222-222222222222/content",
            command.AltText));
    }

    public Task<DocumentImageContentResult> GetContentAsync(GetDocumentImageContentCommand command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        LastGetCommand = command;
        Stream content = new MemoryStream([0x89, 0x50, 0x4e, 0x47]);
        return Task.FromResult(new DocumentImageContentResult("diagram.png", "image/png", 4, content));
    }
}

public sealed class DocumentImageFakeAuthService : IAuthService
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
        ct.ThrowIfCancellationRequested();
        AuthenticatedUser? user = _users.Values.SingleOrDefault(item =>
            item.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(password == ValidPassword ? user : null);
    }

    public Task<AuthenticatedUser?> GetActiveUserAsync(Guid userId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_users.GetValueOrDefault(userId));
    }
}
