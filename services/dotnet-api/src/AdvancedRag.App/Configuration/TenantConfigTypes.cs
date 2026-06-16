namespace AdvancedRag.App.Configuration;

public sealed record TenantConfig(
    Guid Id,
    string BrandName,
    string? BrandLogoUrl,
    string? BrandFaviconUrl,
    string PrimaryColor,
    string DefaultLocale,
    IReadOnlyList<string> SupportedLocales,
    string LlmProvider,
    string LlmModel,
    string? LlmBaseUrl,
    string EmbeddingProvider,
    string EmbeddingModel,
    int EmbeddingDimensions,
    string RerankerProvider,
    string RerankerModel,
    string? RerankerBaseUrl,
    bool EnableBm25,
    bool EnableReranker,
    bool EnableConversationalMemory,
    bool EnableQueryRewrite,
    int RagTopKVector,
    int RagTopKBm25,
    int RagTopKFinal,
    int RrfK,
    int ConversationHistoryTurns,
    int CacheTtlHours,
    decimal CacheSimilarityThreshold,
    decimal DefaultMonthlyBudgetUsd,
    decimal? GlobalDailyBudgetUsd,
    bool EnableVlmImageDescription,
    bool EnableOtel,
    string? S3Endpoint,
    string S3Bucket,
    string S3Region,
    string CustomerTimezone,
    int ImportMaxFileSizeMb,
    int ChatMaxQuestionChars,
    bool SeededFromEnv,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record TenantConfigDraft(
    string BrandName,
    string? BrandLogoUrl,
    string? BrandFaviconUrl,
    string PrimaryColor,
    string DefaultLocale,
    IReadOnlyList<string> SupportedLocales,
    string LlmProvider,
    string LlmModel,
    string? LlmBaseUrl,
    string EmbeddingProvider,
    string EmbeddingModel,
    int EmbeddingDimensions,
    string RerankerProvider,
    string RerankerModel,
    string? RerankerBaseUrl,
    bool EnableBm25,
    bool EnableReranker,
    bool EnableConversationalMemory,
    bool EnableQueryRewrite,
    int RagTopKVector,
    int RagTopKBm25,
    int RagTopKFinal,
    int RrfK,
    int ConversationHistoryTurns,
    int CacheTtlHours,
    decimal CacheSimilarityThreshold,
    decimal DefaultMonthlyBudgetUsd,
    decimal? GlobalDailyBudgetUsd,
    bool EnableVlmImageDescription,
    bool EnableOtel,
    string? S3Endpoint,
    string S3Bucket,
    string S3Region,
    string CustomerTimezone,
    int ImportMaxFileSizeMb,
    int ChatMaxQuestionChars,
    bool SeededFromEnv)
{
    public static TenantConfigDraft CreateDefault()
    {
        return new TenantConfigDraft(
            "Help Center",
            null,
            null,
            "#2563eb",
            "es-AR",
            ["es-AR"],
            "openai",
            "gpt-4.1-nano",
            null,
            "openai",
            "text-embedding-3-small",
            1024,
            "tei-bge",
            "BAAI/bge-reranker-v2-m3",
            null,
            true,
            true,
            true,
            false,
            20,
            20,
            8,
            60,
            5,
            24,
            0.90m,
            5.00m,
            null,
            false,
            false,
            null,
            "helpcenter",
            "us-east-1",
            "America/Argentina/Buenos_Aires",
            10,
            4000,
            false);
    }
}

public interface ITenantConfigService
{
    Task<TenantConfig> GetAsync(CancellationToken ct);

    Task<TenantConfig> UpdateAsync(TenantConfigDraft draft, CancellationToken ct);
}

public interface ITenantConfigRepository
{
    Task<TenantConfig> GetAsync(CancellationToken ct);

    Task<TenantConfig> UpsertAsync(TenantConfigDraft draft, CancellationToken ct);
}

public sealed class TenantConfigService : ITenantConfigService
{
    private readonly ITenantConfigRepository _repository;

    public TenantConfigService(ITenantConfigRepository repository)
    {
        _repository = repository;
    }

    public Task<TenantConfig> GetAsync(CancellationToken ct)
    {
        return _repository.GetAsync(ct);
    }

    public Task<TenantConfig> UpdateAsync(TenantConfigDraft draft, CancellationToken ct)
    {
        TenantConfigDraft normalized = Normalize(draft);
        return _repository.UpsertAsync(normalized, ct);
    }

    private static TenantConfigDraft Normalize(TenantConfigDraft draft)
    {
        string brandName = Required(draft.BrandName, "brandName");
        string primaryColor = Required(draft.PrimaryColor, "primaryColor");
        string defaultLocale = Required(draft.DefaultLocale, "defaultLocale");
        string[] supportedLocales = draft.SupportedLocales
            .Select(locale => Required(locale, "supportedLocales"))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (!supportedLocales.Contains(defaultLocale, StringComparer.Ordinal))
        {
            throw new TenantConfigException(
                "VALIDATION_FAILED",
                400,
                "Default locale must be included in supported locales.",
                new Dictionary<string, object?> { ["field"] = "defaultLocale" });
        }

        if (draft.EmbeddingDimensions <= 0
            || draft.RagTopKVector <= 0
            || draft.RagTopKBm25 <= 0
            || draft.RagTopKFinal <= 0
            || draft.RrfK <= 0
            || draft.ConversationHistoryTurns < 0
            || draft.CacheTtlHours <= 0
            || draft.CacheSimilarityThreshold <= 0m
            || draft.CacheSimilarityThreshold > 1m
            || draft.DefaultMonthlyBudgetUsd < 0m
            || draft.GlobalDailyBudgetUsd < 0m
            || draft.ImportMaxFileSizeMb <= 0
            || draft.ChatMaxQuestionChars <= 0)
        {
            throw new TenantConfigException(
                "VALIDATION_FAILED",
                400,
                "Tenant configuration contains invalid numeric values.");
        }

        return draft with
        {
            BrandName = brandName,
            BrandLogoUrl = EmptyToNull(draft.BrandLogoUrl),
            BrandFaviconUrl = EmptyToNull(draft.BrandFaviconUrl),
            PrimaryColor = primaryColor,
            DefaultLocale = defaultLocale,
            SupportedLocales = supportedLocales,
            LlmProvider = Required(draft.LlmProvider, "llmProvider"),
            LlmModel = Required(draft.LlmModel, "llmModel"),
            LlmBaseUrl = EmptyToNull(draft.LlmBaseUrl),
            EmbeddingProvider = Required(draft.EmbeddingProvider, "embeddingProvider"),
            EmbeddingModel = Required(draft.EmbeddingModel, "embeddingModel"),
            RerankerProvider = Required(draft.RerankerProvider, "rerankerProvider"),
            RerankerModel = Required(draft.RerankerModel, "rerankerModel"),
            RerankerBaseUrl = EmptyToNull(draft.RerankerBaseUrl),
            S3Endpoint = EmptyToNull(draft.S3Endpoint),
            S3Bucket = Required(draft.S3Bucket, "s3Bucket"),
            S3Region = Required(draft.S3Region, "s3Region"),
            CustomerTimezone = Required(draft.CustomerTimezone, "customerTimezone"),
        };
    }

    private static string Required(string value, string field)
    {
        string normalized = value.Trim();
        if (normalized.Length == 0)
        {
            throw new TenantConfigException(
                "VALIDATION_FAILED",
                400,
                $"{field} is required.",
                new Dictionary<string, object?> { ["field"] = field });
        }

        return normalized;
    }

    private static string? EmptyToNull(string? value)
    {
        string? normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}

public sealed class TenantConfigException : Exception
{
    public TenantConfigException(
        string code,
        int httpStatus,
        string message,
        IReadOnlyDictionary<string, object?>? details = null)
        : base(message)
    {
        Code = code;
        HttpStatus = httpStatus;
        Details = details ?? new Dictionary<string, object?>();
    }

    public string Code { get; }

    public int HttpStatus { get; }

    public IReadOnlyDictionary<string, object?> Details { get; }
}
