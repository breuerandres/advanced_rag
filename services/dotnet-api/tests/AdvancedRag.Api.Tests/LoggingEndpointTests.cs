using System.Net;
using System.Text.Json;
using AdvancedRag.Api.Health;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AdvancedRag.Api.Tests;

public sealed class LoggingEndpointTests
{
    [Fact]
    public async Task RequestLog_IncludesOperationalFieldsAndSafeErrorCode()
    {
        string directory = Path.Combine(Path.GetTempPath(), "advanced-rag-dotnet-logs", Guid.NewGuid().ToString("N"));
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Logging:Directory"] = directory,
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IOperationalReadinessChecker>();
                services.AddSingleton<IOperationalReadinessChecker>(
                    new FakeReadinessChecker(OperationalReadinessResult.Ready));
            });
        });
        using HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/missing");
        request.Headers.Add("X-Request-ID", "req-log-dotnet");
        request.Headers.Add("X-Forwarded-For", "198.51.100.22");

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        string logPath = Directory.GetFiles(directory, "log-*.json").Single();
        string line = await File.ReadAllTextAsync(logPath);
        using JsonDocument document = JsonDocument.Parse(line);
        JsonElement root = document.RootElement;
        root.GetProperty("request_id").GetString().Should().Be("req-log-dotnet");
        root.GetProperty("origin_ip").GetString().Should().Be("198.51.100.22");
        root.GetProperty("route").GetString().Should().Be("/missing");
        root.GetProperty("response_status").GetInt32().Should().Be(404);
        root.GetProperty("safe_error_code").GetString().Should().Be("NOT_FOUND");
        root.GetProperty("timestamp").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RequestLog_AllowsConcurrentWritesToTheSameDailyFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), "advanced-rag-dotnet-logs", Guid.NewGuid().ToString("N"));
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Logging:Directory"] = directory,
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IOperationalReadinessChecker>();
                services.AddSingleton<IOperationalReadinessChecker>(
                    new FakeReadinessChecker(OperationalReadinessResult.Ready));
            });
        });
        using HttpClient client = factory.CreateClient();

        Task<HttpResponseMessage>[] requests = Enumerable.Range(0, 20)
            .Select(index =>
            {
                HttpRequestMessage request = new(HttpMethod.Get, $"/missing-{index}");
                request.Headers.Add("X-Request-ID", $"req-log-concurrent-{index}");
                return client.SendAsync(request);
            })
            .ToArray();

        HttpResponseMessage[] responses = await Task.WhenAll(requests);

        responses.Should().AllSatisfy(response => response.StatusCode.Should().Be(HttpStatusCode.NotFound));
        string logPath = Directory.GetFiles(directory, "log-*.json").Single();
        string[] lines = await File.ReadAllLinesAsync(logPath);
        lines.Should().HaveCount(20);
        responses.ToList().ForEach(response => response.Dispose());
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
}
