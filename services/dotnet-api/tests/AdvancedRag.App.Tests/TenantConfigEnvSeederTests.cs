using AdvancedRag.App.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AdvancedRag.App.Tests;

public sealed class TenantConfigEnvSeederTests
{
    private sealed class StubRepo : ITenantConfigRepository
    {
        public TenantConfig Current { get; set; } = TenantConfigDraft.CreateDefault().ToConfigForTest();
        public TenantConfigDraft? Upserted { get; private set; }
        public Task<TenantConfig> GetAsync(CancellationToken ct) => Task.FromResult(Current);
        public Task<TenantConfig> UpsertAsync(TenantConfigDraft draft, CancellationToken ct)
        {
            Upserted = draft;
            return Task.FromResult(Current);
        }
    }

    [Fact]
    public async Task SeedAsync_WhenNotSeeded_CopiesEnvValuesAndSetsFlag()
    {
        var repo = new StubRepo();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OPENAI_CHAT_MODEL"] = "gpt-4.1-nano",
                ["CUSTOMER_TIMEZONE"] = "UTC",
                ["DEFAULT_MONTHLY_AI_BUDGET_USD"] = "7.50",
                ["RAG_SEMANTIC_CACHE_TTL_HOURS"] = "12",
                ["RAG_SEMANTIC_CACHE_SIMILARITY_THRESHOLD"] = "0.85",
                ["CHAT_MAX_QUESTION_CHARS"] = "3000",
                ["IMPORT_MAX_FILE_SIZE_MB"] = "20",
            })
            .Build();
        var seeder = new TenantConfigEnvSeeder(repo, config);

        await seeder.SeedAsync(CancellationToken.None);

        repo.Upserted.Should().NotBeNull();
        repo.Upserted!.LlmModel.Should().Be("gpt-4.1-nano");
        repo.Upserted.CustomerTimezone.Should().Be("UTC");
        repo.Upserted.DefaultMonthlyBudgetUsd.Should().Be(7.50m);
        repo.Upserted.CacheTtlHours.Should().Be(12);
        repo.Upserted.CacheSimilarityThreshold.Should().Be(0.85m);
        repo.Upserted.ChatMaxQuestionChars.Should().Be(3000);
        repo.Upserted.ImportMaxFileSizeMb.Should().Be(20);
        repo.Upserted.SeededFromEnv.Should().BeTrue();
    }

    [Fact]
    public async Task SeedAsync_WhenAlreadySeeded_DoesNothing()
    {
        var repo = new StubRepo();
        repo.Current = repo.Current with { SeededFromEnv = true };
        var seeder = new TenantConfigEnvSeeder(repo, new ConfigurationBuilder().Build());

        await seeder.SeedAsync(CancellationToken.None);

        repo.Upserted.Should().BeNull();
    }
}

internal static class TenantConfigTestExtensions
{
    public static TenantConfig ToConfigForTest(this TenantConfigDraft d) => new(
        Guid.NewGuid(), d.BrandName, d.BrandLogoUrl, d.BrandFaviconUrl, d.PrimaryColor,
        d.DefaultLocale, d.SupportedLocales, d.LlmProvider, d.LlmModel, d.LlmBaseUrl,
        d.EmbeddingProvider, d.EmbeddingModel, d.EmbeddingDimensions, d.RerankerProvider,
        d.RerankerModel, d.RerankerBaseUrl, d.EnableBm25, d.EnableReranker,
        d.EnableConversationalMemory, d.EnableQueryRewrite, d.RagTopKVector, d.RagTopKBm25,
        d.RagTopKFinal, d.RrfK, d.ConversationHistoryTurns, d.CacheTtlHours,
        d.CacheSimilarityThreshold, d.DefaultMonthlyBudgetUsd, d.GlobalDailyBudgetUsd,
        d.EnableVlmImageDescription, d.EnableOtel, d.S3Endpoint, d.S3Bucket, d.S3Region,
        d.CustomerTimezone, d.ImportMaxFileSizeMb, d.ChatMaxQuestionChars, d.SeededFromEnv,
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
}
