namespace AdvancedRag.Api.Models.Configuration;

public sealed record UpdateConfigurationRequest(
    string ChatModel,
    string CustomerTimezone,
    decimal DefaultMonthlyAiBudgetUsd,
    int SemanticCacheTtlHours,
    decimal SemanticCacheSimilarityThreshold,
    int ChatMaxQuestionChars,
    int ImportMaxFileSizeMb);
