using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace AdvancedRag.App.Configuration;

/// <summary>
/// One-time reconciliation that copies the deployment's environment values into the
/// singleton tenant_config row before it becomes the authoritative source for the
/// operational settings screen. Guarded by SeededFromEnv so it never overwrites later
/// admin edits.
/// </summary>
public sealed class TenantConfigEnvSeeder
{
    private readonly ITenantConfigRepository _repository;
    private readonly IConfiguration _configuration;

    public TenantConfigEnvSeeder(ITenantConfigRepository repository, IConfiguration configuration)
    {
        _repository = repository;
        _configuration = configuration;
    }

    public async Task SeedAsync(CancellationToken ct)
    {
        TenantConfig current = await _repository.GetAsync(ct);
        if (current.SeededFromEnv)
        {
            return;
        }

        TenantConfigDraft draft = new(
            current.BrandName, current.BrandLogoUrl, current.BrandFaviconUrl, current.PrimaryColor,
            current.DefaultLocale, current.SupportedLocales, current.LlmProvider,
            Env("OPENAI_CHAT_MODEL", current.LlmModel), current.LlmBaseUrl,
            current.EmbeddingProvider,
            Env("OPENAI_EMBEDDING_MODEL", current.EmbeddingModel),
            EnvInt("OPENAI_EMBEDDING_DIMENSIONS", current.EmbeddingDimensions),
            current.RerankerProvider, current.RerankerModel, current.RerankerBaseUrl,
            current.EnableBm25, current.EnableReranker, current.EnableConversationalMemory,
            current.EnableQueryRewrite, current.RagTopKVector, current.RagTopKBm25,
            current.RagTopKFinal, current.RrfK, current.ConversationHistoryTurns,
            EnvInt("RAG_SEMANTIC_CACHE_TTL_HOURS", current.CacheTtlHours),
            EnvDecimal("RAG_SEMANTIC_CACHE_SIMILARITY_THRESHOLD", current.CacheSimilarityThreshold),
            EnvDecimal("DEFAULT_MONTHLY_AI_BUDGET_USD", current.DefaultMonthlyBudgetUsd),
            current.GlobalDailyBudgetUsd, current.EnableVlmImageDescription, current.EnableOtel,
            current.S3Endpoint, current.S3Bucket, current.S3Region,
            Env("CUSTOMER_TIMEZONE", current.CustomerTimezone),
            EnvInt("IMPORT_MAX_FILE_SIZE_MB", current.ImportMaxFileSizeMb),
            EnvInt("CHAT_MAX_QUESTION_CHARS", current.ChatMaxQuestionChars),
            true);

        await _repository.UpsertAsync(draft, ct);
    }

    private string Env(string key, string fallback)
    {
        string? value = _configuration[key];
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private int EnvInt(string key, int fallback)
        => int.TryParse(_configuration[key], out int parsed) ? parsed : fallback;

    private decimal EnvDecimal(string key, decimal fallback)
        => decimal.TryParse(_configuration[key], NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed)
            ? parsed
            : fallback;
}
