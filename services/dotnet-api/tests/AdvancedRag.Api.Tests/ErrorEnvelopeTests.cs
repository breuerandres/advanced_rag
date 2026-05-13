using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AdvancedRag.Api.Tests;

public sealed class ErrorEnvelopeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ErrorEnvelopeTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UnknownRoute_ReturnsSharedErrorEnvelope()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/missing-route");
        request.Headers.Add("X-Request-ID", "test-request-id");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Headers.GetValues("X-Request-ID").Should().ContainSingle("test-request-id");

        var body = await response.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        body.Should().BeEquivalentTo(
            new ApiErrorEnvelope(
                new ApiErrorBody(
                    "NOT_FOUND",
                    "Resource not found.",
                    new Dictionary<string, object>(),
                    "test-request-id")));
    }

    private sealed record ApiErrorEnvelope(ApiErrorBody Error);

    private sealed record ApiErrorBody(
        string Code,
        string Message,
        Dictionary<string, object> Details,
        string RequestId);
}
