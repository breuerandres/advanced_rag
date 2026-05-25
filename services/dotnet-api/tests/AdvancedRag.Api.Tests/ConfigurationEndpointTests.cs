using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AdvancedRag.Api.Tests;

public sealed class ConfigurationEndpointTests
    : IClassFixture<UserAdministrationWebApplicationFactory>
{
    private readonly UserAdministrationWebApplicationFactory _factory;

    public ConfigurationEndpointTests(UserAdministrationWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetConfiguration_AsAdmin_ReturnsOperationalDefaultsWithoutSecretValues()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        string sessionCookie = await LoginAsync(client);

        using HttpRequestMessage request = new(HttpMethod.Get, "/api/configuration");
        request.Headers.Host = "manage.localhost";
        request.Headers.Add("Cookie", sessionCookie);

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ConfigurationResponse? body = await response.Content.ReadFromJsonAsync<ConfigurationResponse>();
        body.Should().NotBeNull();
        body!.CustomerTimezone.Should().Be("America/Argentina/Buenos_Aires");
        body.ChatModel.Should().Be("gpt-4.1-nano");
        body.EmbeddingModel.Should().Be("text-embedding-3-small");
        body.EmbeddingDimensions.Should().Be(1536);
        body.DefaultMonthlyAiBudgetUsd.Should().Be(5m);
        body.SemanticCacheTtlHours.Should().Be(24);
        body.SemanticCacheSimilarityThreshold.Should().Be(0.90m);
        body.Secrets.Should().Contain(secret => secret.Name == "OpenAI API key" && secret.Status == "Configured");
        body.Secrets.Should().Contain(secret => secret.Name == "Internal service token" && secret.Status == "Configured");
        string rawBody = await response.Content.ReadAsStringAsync();
        rawBody.Should().NotContain("sk-");
    }

    private static async Task<string> LoginAsync(HttpClient client)
    {
        CsrfState csrf = await GetCsrfAsync(client);
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new
            {
                email = FakeAuthService.AdminEmail,
                password = FakeAuthService.ValidPassword,
            }),
        };
        request.Headers.Host = "manage.localhost";
        request.Headers.Add("X-CSRF-Token", csrf.Token);
        request.Headers.Add("Cookie", csrf.Cookie);

        using HttpResponseMessage response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return CookiePair(GetSetCookie(response, "__Host-session"));
    }

    private static async Task<CsrfState> GetCsrfAsync(HttpClient client)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/csrf");
        request.Headers.Host = "manage.localhost";

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.TryGetValues("X-CSRF-Token", out IEnumerable<string>? tokenValues).Should().BeTrue();
        string token = tokenValues!.Single();
        return new CsrfState(token, CookiePair(GetSetCookie(response, "__Host-CSRF")));
    }

    private static string GetSetCookie(HttpResponseMessage response, string cookieName)
    {
        response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values).Should().BeTrue();
        return values!.Single(value => value.StartsWith(cookieName, StringComparison.Ordinal));
    }

    private static string CookiePair(string setCookie)
    {
        return setCookie.Split(';', 2)[0];
    }

    private sealed record CsrfState(string Token, string Cookie);

    private sealed record ConfigurationResponse(
        string CustomerTimezone,
        string ChatModel,
        string EmbeddingModel,
        int EmbeddingDimensions,
        decimal DefaultMonthlyAiBudgetUsd,
        int SemanticCacheTtlHours,
        decimal SemanticCacheSimilarityThreshold,
        int ChatMaxQuestionChars,
        int ImportMaxFileSizeMb,
        IReadOnlyList<SecretConfigurationStatusResponse> Secrets);

    private sealed record SecretConfigurationStatusResponse(string Name, string Status);
}
