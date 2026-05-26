using AdvancedRag.App.Configuration;

namespace AdvancedRag.Api.Models.Configuration;

public sealed record UpdateTenantConfigRequest(
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
    string S3Region)
{
    public TenantConfigDraft ToDraft()
    {
        return new TenantConfigDraft(
            BrandName,
            BrandLogoUrl,
            BrandFaviconUrl,
            PrimaryColor,
            DefaultLocale,
            SupportedLocales,
            LlmProvider,
            LlmModel,
            LlmBaseUrl,
            EmbeddingProvider,
            EmbeddingModel,
            EmbeddingDimensions,
            RerankerProvider,
            RerankerModel,
            RerankerBaseUrl,
            EnableBm25,
            EnableReranker,
            EnableConversationalMemory,
            EnableQueryRewrite,
            RagTopKVector,
            RagTopKBm25,
            RagTopKFinal,
            RrfK,
            ConversationHistoryTurns,
            CacheTtlHours,
            CacheSimilarityThreshold,
            DefaultMonthlyBudgetUsd,
            GlobalDailyBudgetUsd,
            EnableVlmImageDescription,
            EnableOtel,
            S3Endpoint,
            S3Bucket,
            S3Region);
    }
}

public sealed record TenantConfigResponse(
    TenantBrandConfigResponse Brand,
    TenantLocaleConfigResponse Locale,
    TenantProviderConfigResponse Providers,
    TenantRetrievalConfigResponse Retrieval,
    TenantFeatureConfigResponse Features,
    TenantBudgetConfigResponse Budgets,
    TenantStorageConfigResponse Storage)
{
    public static TenantConfigResponse FromTenantConfig(TenantConfig config)
    {
        return new TenantConfigResponse(
            new TenantBrandConfigResponse(
                config.BrandName,
                config.BrandLogoUrl,
                config.BrandFaviconUrl,
                config.PrimaryColor),
            new TenantLocaleConfigResponse(config.DefaultLocale, config.SupportedLocales),
            new TenantProviderConfigResponse(
                new TenantLlmConfigResponse(config.LlmProvider, config.LlmModel, config.LlmBaseUrl),
                new TenantEmbeddingConfigResponse(
                    config.EmbeddingProvider,
                    config.EmbeddingModel,
                    config.EmbeddingDimensions),
                new TenantRerankerConfigResponse(
                    config.RerankerProvider,
                    config.RerankerModel,
                    config.RerankerBaseUrl,
                    config.EnableReranker)),
            new TenantRetrievalConfigResponse(
                config.EnableBm25,
                config.RagTopKVector,
                config.RagTopKBm25,
                config.RagTopKFinal,
                config.RrfK,
                config.ConversationHistoryTurns,
                config.CacheTtlHours,
                config.CacheSimilarityThreshold),
            new TenantFeatureConfigResponse(
                config.EnableConversationalMemory,
                config.EnableQueryRewrite,
                config.EnableVlmImageDescription,
                config.EnableOtel),
            new TenantBudgetConfigResponse(
                config.DefaultMonthlyBudgetUsd,
                config.GlobalDailyBudgetUsd),
            new TenantStorageConfigResponse(config.S3Endpoint, config.S3Bucket, config.S3Region));
    }
}

public sealed record TenantBrandConfigResponse(
    string Name,
    string? LogoUrl,
    string? FaviconUrl,
    string PrimaryColor);

public sealed record TenantLocaleConfigResponse(string DefaultLocale, IReadOnlyList<string> SupportedLocales);

public sealed record TenantProviderConfigResponse(
    TenantLlmConfigResponse Llm,
    TenantEmbeddingConfigResponse Embedding,
    TenantRerankerConfigResponse Reranker);

public sealed record TenantLlmConfigResponse(string Provider, string Model, string? BaseUrl);

public sealed record TenantEmbeddingConfigResponse(string Provider, string Model, int Dimensions);

public sealed record TenantRerankerConfigResponse(string Provider, string Model, string? BaseUrl, bool Enabled);

public sealed record TenantRetrievalConfigResponse(
    bool EnableBm25,
    int RagTopKVector,
    int RagTopKBm25,
    int RagTopKFinal,
    int RrfK,
    int ConversationHistoryTurns,
    int CacheTtlHours,
    decimal CacheSimilarityThreshold);

public sealed record TenantFeatureConfigResponse(
    bool EnableConversationalMemory,
    bool EnableQueryRewrite,
    bool EnableVlmImageDescription,
    bool EnableOtel);

public sealed record TenantBudgetConfigResponse(decimal DefaultMonthlyBudgetUsd, decimal? GlobalDailyBudgetUsd);

public sealed record TenantStorageConfigResponse(string? S3Endpoint, string S3Bucket, string S3Region);
