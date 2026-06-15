namespace AdvancedRag.Api.Models.Configuration;

public sealed record ConfigurationResponse(
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

public sealed record SecretConfigurationStatusResponse(string Name, string Status);
