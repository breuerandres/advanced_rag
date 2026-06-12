using System.Net.Http.Json;
using AdvancedRag.App.Documents;

namespace AdvancedRag.Infrastructure.Documents;

public sealed class FastApiInternalCacheInvalidationClient : IInternalCacheInvalidationClient
{
    private readonly HttpClient _http;
    private readonly string _internalServiceToken;

    public FastApiInternalCacheInvalidationClient(HttpClient http, string internalServiceToken)
    {
        _http = http;
        _internalServiceToken = internalServiceToken;
    }

    public async Task<int> InvalidateDocumentsAsync(IReadOnlyList<Guid> documentIds, CancellationToken ct)
    {
        if (documentIds.Count == 0)
        {
            return 0;
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, "/internal/cache-invalidations")
        {
            Content = JsonContent.Create(new CacheInvalidationHttpRequest(documentIds)),
        };
        message.Headers.Add("X-Internal-Service-Token", _internalServiceToken);

        try
        {
            using var response = await _http.SendAsync(message, ct);
            if (!response.IsSuccessStatusCode)
            {
                return 0;
            }

            var body = await response.Content.ReadFromJsonAsync<CacheInvalidationHttpResponse>(cancellationToken: ct);
            return body?.Invalidated ?? 0;
        }
        catch (HttpRequestException)
        {
            return 0; // best-effort: cache entries expire by TTL; do not fail the lifecycle operation
        }
    }

    private sealed record CacheInvalidationHttpRequest(IReadOnlyList<Guid> DocumentIds);

    private sealed record CacheInvalidationHttpResponse(int Invalidated);
}
