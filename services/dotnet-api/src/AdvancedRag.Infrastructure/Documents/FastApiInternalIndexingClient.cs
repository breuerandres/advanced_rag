using System.Net.Http.Json;
using AdvancedRag.App.Documents;

namespace AdvancedRag.Infrastructure.Documents;

public sealed class FastApiInternalIndexingClient : IInternalIndexingClient
{
    private readonly HttpClient _http;
    private readonly string _internalServiceToken;

    public FastApiInternalIndexingClient(HttpClient http, string internalServiceToken)
    {
        _http = http;
        _internalServiceToken = internalServiceToken;
    }

    public async Task<InternalIndexingResult> CreateIndexingJobAsync(
        InternalIndexingRequest request,
        CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/internal/indexing-jobs")
        {
            Content = JsonContent.Create(new InternalIndexingHttpRequest(
                request.InstructionId,
                request.InstructionVersionId,
                request.ContentHtml,
                request.CorpusMode,
                request.Retry)),
        };
        message.Headers.Add("X-Internal-Service-Token", _internalServiceToken);

        using var response = await _http.SendAsync(message, ct);
        if (!response.IsSuccessStatusCode)
        {
            return new InternalIndexingResult(
                Guid.Empty,
                "Failed",
                0,
                "INDEXING_FAILED",
                "Internal indexing request failed.");
        }

        var body = await response.Content.ReadFromJsonAsync<InternalIndexingHttpResponse>(cancellationToken: ct);
        if (body is null)
        {
            return new InternalIndexingResult(
                Guid.Empty,
                "Failed",
                0,
                "INDEXING_FAILED",
                "Internal indexing response was empty.");
        }

        return new InternalIndexingResult(
            body.JobId,
            body.Status,
            body.ChunkCount,
            body.ErrorCode,
            body.ErrorMessage);
    }

    private sealed record InternalIndexingHttpRequest(
        Guid InstructionId,
        Guid InstructionVersionId,
        string ContentHtml,
        string CorpusMode,
        bool Retry);

    private sealed record InternalIndexingHttpResponse(
        Guid JobId,
        string Status,
        int ChunkCount,
        string? ErrorCode,
        string? ErrorMessage);
}
