using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        body.EmbeddingDimensions.Should().Be(1024);
        body.DefaultMonthlyAiBudgetUsd.Should().Be(5m);
        body.SemanticCacheTtlHours.Should().Be(24);
        body.SemanticCacheSimilarityThreshold.Should().Be(0.90m);
        body.Secrets.Should().Contain(secret => secret.Name == "OpenAI API key" && secret.Status == "Configured");
        body.Secrets.Should().Contain(secret => secret.Name == "Internal service token" && secret.Status == "Configured");
        string rawBody = await response.Content.ReadAsStringAsync();
        rawBody.Should().NotContain("sk-");
    }

    [Fact]
    public async Task GetConfiguration_AsViewer_ReturnsSafeReadOnlyOperationalDefaults()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        string sessionCookie = await LoginAsync(client, FakeAuthService.TargetEmail);

        using HttpRequestMessage request = new(HttpMethod.Get, "/api/configuration");
        request.Headers.Host = "manage.localhost";
        request.Headers.Add("Cookie", sessionCookie);

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        string rawBody = await response.Content.ReadAsStringAsync();
        rawBody.Should().Contain("gpt-4.1-nano");
        rawBody.Should().Contain("Configured");
        rawBody.Should().NotContain("sk-");
        rawBody.ToLowerInvariant().Should().NotContain("secret-value");
    }

    [Fact]
    public async Task GetConfiguration_IncludesProviderAndOperationalFields()
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
        body!.LlmProvider.Should().Be("openai");
        body.ChatModel.Should().Be("gpt-4.1-nano");          // from tenant_config (env-seeded)
        body.ImportMaxFileSizeMb.Should().Be(10);
        body.ChatMaxQuestionChars.Should().Be(4000);
        body.EmbeddingModel.Should().Be("text-embedding-3-small"); // still env-sourced
    }

    [Fact]
    public async Task GetV1Config_WithoutSession_ReturnsPublicSafeTenantConfigWithoutSecrets()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/config");
        request.Headers.Host = "manage.localhost";

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        TenantConfigResponse? body = await response.Content.ReadFromJsonAsync<TenantConfigResponse>();
        body.Should().NotBeNull();
        body!.Brand.Name.Should().Be("Help Center");
        body.Brand.PrimaryColor.Should().Be("#2563eb");
        body.Locale.DefaultLocale.Should().Be("es-AR");
        body.Locale.SupportedLocales.Should().Equal("es-AR", "en-US", "pt-BR");
        body.Providers.Llm.Provider.Should().Be("openai");
        body.Providers.Llm.Model.Should().Be("gpt-4.1-nano");
        body.Providers.Llm.BaseUrl.Should().BeNull();
        body.Providers.Embedding.Dimensions.Should().Be(1024);
        body.Providers.Reranker.Enabled.Should().BeTrue();
        body.Retrieval.EnableBm25.Should().BeTrue();
        body.Features.EnableConversationalMemory.Should().BeTrue();
        body.Budgets.DefaultMonthlyBudgetUsd.Should().Be(5m);

        string rawBody = await response.Content.ReadAsStringAsync();
        rawBody.Should().NotContain("sk-");
        rawBody.ToLowerInvariant().Should().NotContain("secret");
    }

    [Fact]
    public async Task PutV1Config_AsDocumentManager_ReturnsForbidden()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        string sessionCookie = await LoginAsync(client, FakeAuthService.DocumentManagerEmail);
        CsrfState csrf = await GetCsrfAsync(client);

        using HttpResponseMessage response = await SendJsonAsync(
            client,
            HttpMethod.Put,
            "/api/v1/config",
            new
            {
                brandName = "Internal Docs",
                defaultLocale = "en-US",
                supportedLocales = new[] { "es-AR", "en-US" },
                primaryColor = "#0f766e",
            },
            csrf,
            sessionCookie);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PutV1Config_AsAdmin_PersistsPublicSafeTenantConfig()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        string sessionCookie = await LoginAsync(client, FakeAuthService.AdminEmail);
        CsrfState csrf = await GetCsrfAsync(client);

        using HttpResponseMessage response = await SendJsonAsync(
            client,
            HttpMethod.Put,
            "/api/v1/config",
            new
            {
                brandName = "Knowledge Base",
                primaryColor = "#0f766e",
                defaultLocale = "en-US",
                supportedLocales = new[] { "en-US", "pt-BR" },
                llmProvider = "openai",
                llmModel = "gpt-4o-mini",
                llmBaseUrl = "https://api.openai.com/v1",
                embeddingProvider = "openai",
                embeddingModel = "text-embedding-3-large",
                embeddingDimensions = 1024,
                rerankerProvider = "tei-bge",
                rerankerModel = "BAAI/bge-reranker-v2-m3",
                rerankerBaseUrl = "http://tei-reranker:8080",
                enableBm25 = true,
                enableReranker = false,
                enableConversationalMemory = true,
                enableQueryRewrite = true,
                ragTopKVector = 30,
                ragTopKBm25 = 25,
                ragTopKFinal = 10,
                rrfK = 50,
                conversationHistoryTurns = 8,
                cacheTtlHours = 12,
                cacheSimilarityThreshold = 0.88m,
                defaultMonthlyBudgetUsd = 7.50m,
                globalDailyBudgetUsd = 100m,
                enableVlmImageDescription = true,
                enableOtel = true,
                s3Endpoint = "http://minio:9000",
                s3Bucket = "tenant-assets",
                s3Region = "us-east-1",
            },
            csrf,
            sessionCookie);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        TenantConfigResponse? body = await response.Content.ReadFromJsonAsync<TenantConfigResponse>();
        body.Should().NotBeNull();
        body!.Brand.Name.Should().Be("Knowledge Base");
        body.Brand.PrimaryColor.Should().Be("#0f766e");
        body.Locale.DefaultLocale.Should().Be("en-US");
        body.Locale.SupportedLocales.Should().Equal("en-US", "pt-BR");
        body.Providers.Reranker.Enabled.Should().BeFalse();
        body.Retrieval.RagTopKFinal.Should().Be(10);
        body.Features.EnableQueryRewrite.Should().BeTrue();
        body.Budgets.DefaultMonthlyBudgetUsd.Should().Be(7.50m);
        body.Storage.S3Endpoint.Should().Be("http://minio:9000");
    }

    [Fact]
    public async Task PutConfiguration_AsAdmin_PersistsAndReturnsUpdated()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        string sessionCookie = await LoginAsync(client);
        CsrfState csrf = await GetCsrfAsync(client);

        using HttpResponseMessage response = await SendJsonAsync(
            client, HttpMethod.Put, "/api/configuration",
            new
            {
                chatModel = "gpt-4.1-mini",
                customerTimezone = "UTC",
                defaultMonthlyAiBudgetUsd = 9.00m,
                semanticCacheTtlHours = 48,
                semanticCacheSimilarityThreshold = 0.88m,
                chatMaxQuestionChars = 2500,
                importMaxFileSizeMb = 15,
            },
            csrf, sessionCookie);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ConfigurationResponse? body = await response.Content.ReadFromJsonAsync<ConfigurationResponse>();
        body.Should().NotBeNull();
        body!.ChatModel.Should().Be("gpt-4.1-mini");
        body.ImportMaxFileSizeMb.Should().Be(15);
        body.SemanticCacheSimilarityThreshold.Should().Be(0.88m);
    }

    [Fact]
    public async Task PutConfiguration_AsViewer_ReturnsForbidden()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        string sessionCookie = await LoginAsync(client, FakeAuthService.TargetEmail);
        CsrfState csrf = await GetCsrfAsync(client);

        using HttpResponseMessage response = await SendJsonAsync(
            client, HttpMethod.Put, "/api/configuration",
            new
            {
                chatModel = "x",
                customerTimezone = "UTC",
                defaultMonthlyAiBudgetUsd = 5m,
                semanticCacheTtlHours = 24,
                semanticCacheSimilarityThreshold = 0.9m,
                chatMaxQuestionChars = 4000,
                importMaxFileSizeMb = 10,
            },
            csrf, sessionCookie);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PutConfiguration_WithInvalidThreshold_ReturnsValidationFailed()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        string sessionCookie = await LoginAsync(client);
        CsrfState csrf = await GetCsrfAsync(client);

        using HttpResponseMessage response = await SendJsonAsync(
            client, HttpMethod.Put, "/api/configuration",
            new
            {
                chatModel = "gpt-4.1-nano",
                customerTimezone = "UTC",
                defaultMonthlyAiBudgetUsd = 5m,
                semanticCacheTtlHours = 24,
                semanticCacheSimilarityThreshold = 1.5m,
                chatMaxQuestionChars = 4000,
                importMaxFileSizeMb = 10,
            },
            csrf, sessionCookie);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task<string> LoginAsync(HttpClient client, string email = FakeAuthService.AdminEmail)
    {
        CsrfState csrf = await GetCsrfAsync(client);
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new
            {
                email,
                password = FakeAuthService.ValidPassword,
            }),
        };
        request.Headers.Host = "manage.localhost";
        request.Headers.Add("X-CSRF-Token", csrf.Token);
        request.Headers.Add("Cookie", csrf.Cookie);
        request.Headers.Add("X-Forwarded-For", $"198.51.100.{Interlocked.Increment(ref _loginIpCounter)}");

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

    private static async Task<HttpResponseMessage> SendJsonAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        object body,
        CsrfState csrf,
        string sessionCookie)
    {
        HttpRequestMessage request = new(method, path)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Host = "manage.localhost";
        request.Headers.Add("X-CSRF-Token", csrf.Token);
        request.Headers.Add("Cookie", $"{csrf.Cookie}; {sessionCookie}");
        return await client.SendAsync(request);
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

    private static int _loginIpCounter;

    private sealed record ConfigurationResponse(
        string CustomerTimezone,
        string LlmProvider,
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

    private sealed record TenantConfigResponse(
        TenantBrandConfigResponse Brand,
        TenantLocaleConfigResponse Locale,
        TenantProviderConfigResponse Providers,
        TenantRetrievalConfigResponse Retrieval,
        TenantFeatureConfigResponse Features,
        TenantBudgetConfigResponse Budgets,
        TenantStorageConfigResponse Storage);

    private sealed record TenantBrandConfigResponse(
        string Name,
        string? LogoUrl,
        string? FaviconUrl,
        string PrimaryColor);

    private sealed record TenantLocaleConfigResponse(string DefaultLocale, IReadOnlyList<string> SupportedLocales);

    private sealed record TenantProviderConfigResponse(
        TenantLlmConfigResponse Llm,
        TenantEmbeddingConfigResponse Embedding,
        TenantRerankerConfigResponse Reranker);

    private sealed record TenantLlmConfigResponse(string Provider, string Model, string? BaseUrl);

    private sealed record TenantEmbeddingConfigResponse(string Provider, string Model, int Dimensions);

    private sealed record TenantRerankerConfigResponse(string Provider, string Model, string? BaseUrl, bool Enabled);

    private sealed record TenantRetrievalConfigResponse(
        bool EnableBm25,
        int RagTopKVector,
        int RagTopKBm25,
        int RagTopKFinal,
        int RrfK,
        int ConversationHistoryTurns,
        int CacheTtlHours,
        decimal CacheSimilarityThreshold);

    private sealed record TenantFeatureConfigResponse(
        bool EnableConversationalMemory,
        bool EnableQueryRewrite,
        bool EnableVlmImageDescription,
        bool EnableOtel);

    private sealed record TenantBudgetConfigResponse(decimal DefaultMonthlyBudgetUsd, decimal? GlobalDailyBudgetUsd);

    private sealed record TenantStorageConfigResponse(string? S3Endpoint, string S3Bucket, string S3Region);
}
