using System.Net;
using System.Net.Http.Json;
using AdvancedRag.Api.Health;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AdvancedRag.Api.Tests;

public sealed class HealthEndpointTests
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthEndpoint_ReturnsOkStatus(string path)
    {
        using WebApplicationFactory<Program> factory = CreateFactory(OperationalReadinessResult.Ready);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        HealthResponse? body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        body.Should().BeEquivalentTo(new HealthResponse("ok", null));
    }

    [Fact]
    public async Task ReadyHealth_WhenDependencyFails_ReturnsServiceUnavailable()
    {
        using WebApplicationFactory<Program> factory = CreateFactory(
            new OperationalReadinessResult(false, ["database", "jwt_signing_keys"]));
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        HealthResponse? body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        body!.Status.Should().Be("unhealthy");
        body.Checks.Should().BeEquivalentTo(["database", "jwt_signing_keys"]);
    }

    [Fact]
    public async Task LiveHealth_WhenReadinessFails_StillReturnsOk()
    {
        using WebApplicationFactory<Program> factory = CreateFactory(
            new OperationalReadinessResult(false, ["database"]));
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static WebApplicationFactory<Program> CreateFactory(OperationalReadinessResult result)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IOperationalReadinessChecker>();
                services.AddSingleton<IOperationalReadinessChecker>(new FakeReadinessChecker(result));
            });
        });
    }

    private sealed class FakeReadinessChecker : IOperationalReadinessChecker
    {
        private readonly OperationalReadinessResult _result;

        public FakeReadinessChecker(OperationalReadinessResult result)
        {
            _result = result;
        }

        public Task<OperationalReadinessResult> CheckAsync(CancellationToken ct)
        {
            return Task.FromResult(_result);
        }
    }

    private sealed record HealthResponse(string Status, IReadOnlyList<string>? Checks);
}
