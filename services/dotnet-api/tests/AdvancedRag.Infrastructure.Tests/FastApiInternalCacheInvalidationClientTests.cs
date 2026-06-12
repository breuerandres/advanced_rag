using System.Net;
using System.Text;
using AdvancedRag.Infrastructure.Documents;
using Xunit;

namespace AdvancedRag.Infrastructure.Tests;

public sealed class FastApiInternalCacheInvalidationClientTests
{
    [Fact]
    public async Task PostsDocumentIdsWithInternalToken()
    {
        string? capturedPath = null;
        string? capturedToken = null;
        string capturedBody = string.Empty;
        var handler = new StubHandler(async (request, ct) =>
        {
            capturedPath = request.RequestUri!.AbsolutePath;
            capturedToken = request.Headers.GetValues("X-Internal-Service-Token").Single();
            capturedBody = await request.Content!.ReadAsStringAsync(ct);
            return Respond(HttpStatusCode.OK, "{\"invalidated\": 3}");
        });
        var client = new FastApiInternalCacheInvalidationClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://rag-api:8000") },
            "token-123");

        int invalidated = await client.InvalidateDocumentsAsync(
            new List<Guid> { Guid.NewGuid() }, CancellationToken.None);

        Assert.Equal(3, invalidated);
        Assert.Equal("/internal/cache-invalidations", capturedPath);
        Assert.Equal("token-123", capturedToken);
        Assert.Contains("documentIds", capturedBody); // camelCase from JsonContent web defaults
    }

    [Fact]
    public async Task ReturnsZeroOnHttpFailure()
    {
        var handler = new StubHandler((_, _) =>
            Task.FromResult(Respond(HttpStatusCode.InternalServerError, "{}")));
        var client = new FastApiInternalCacheInvalidationClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://rag-api:8000") },
            "token-123");

        int invalidated = await client.InvalidateDocumentsAsync(
            new List<Guid> { Guid.NewGuid() }, CancellationToken.None);

        Assert.Equal(0, invalidated); // best-effort: failure must not throw
    }

    [Fact]
    public async Task ReturnsZeroForEmptyDocumentListWithoutCallingHttp()
    {
        bool called = false;
        var handler = new StubHandler((_, _) =>
        {
            called = true;
            return Task.FromResult(Respond(HttpStatusCode.OK, "{\"invalidated\": 1}"));
        });
        var client = new FastApiInternalCacheInvalidationClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://rag-api:8000") },
            "token-123");

        int invalidated = await client.InvalidateDocumentsAsync(Array.Empty<Guid>(), CancellationToken.None);

        Assert.Equal(0, invalidated);
        Assert.False(called);
    }

    private static HttpResponseMessage Respond(HttpStatusCode status, string json)
        => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
            => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => _responder(request, cancellationToken);
    }
}
