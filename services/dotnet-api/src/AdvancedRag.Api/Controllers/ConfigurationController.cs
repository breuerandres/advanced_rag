using AdvancedRag.Api.Models.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace AdvancedRag.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,DocumentManager")]
[Route("api/configuration")]
public sealed class ConfigurationController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public ConfigurationController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet]
    public IActionResult Get()
    {
        ConfigurationResponse response = new(
            CustomerTimezone: _configuration["CUSTOMER_TIMEZONE"]
                ?? _configuration["Customer:Timezone"]
                ?? "UTC",
            ChatModel: _configuration["OPENAI_CHAT_MODEL"]
                ?? _configuration["OpenAI:ChatModel"]
                ?? "gpt-4.1-nano",
            EmbeddingModel: _configuration["OPENAI_EMBEDDING_MODEL"]
                ?? _configuration["OpenAI:EmbeddingModel"]
                ?? "text-embedding-3-small",
            EmbeddingDimensions: GetInt("OPENAI_EMBEDDING_DIMENSIONS", "OpenAI:EmbeddingDimensions", 1536),
            DefaultMonthlyAiBudgetUsd: GetDecimal(
                "DEFAULT_MONTHLY_AI_BUDGET_USD",
                "AiBudget:DefaultMonthlyUsd",
                5m),
            SemanticCacheTtlHours: GetInt(
                "RAG_SEMANTIC_CACHE_TTL_HOURS",
                "Rag:SemanticCacheTtlHours",
                24),
            SemanticCacheSimilarityThreshold: GetDecimal(
                "RAG_SEMANTIC_CACHE_SIMILARITY_THRESHOLD",
                "Rag:SemanticCacheSimilarityThreshold",
                0.90m),
            ChatMaxQuestionChars: GetInt("CHAT_MAX_QUESTION_CHARS", "Chat:MaxQuestionChars", 4000),
            ImportMaxFileSizeMb: 10,
            Secrets:
            [
                SecretStatus(
                    "OpenAI API key",
                    ["OPENAI_API_KEY", "OpenAI:ApiKey"],
                    ["OPENAI_API_KEY_FILE", "OpenAI:ApiKeyFile"]),
                SecretStatus(
                    "JWT signing keys",
                    ["JWT_SIGNING_KEYS_JSON", "Jwt:SigningKeysJson"],
                    ["JWT_SIGNING_KEYS_FILE", "Jwt:SigningKeysFile"]),
                SecretStatus(
                    "CSRF signing key",
                    ["CSRF_SIGNING_KEY", "Csrf:SigningKey"],
                    ["CSRF_SIGNING_KEY_FILE", "Csrf:SigningKeyFile"]),
                SecretStatus(
                    "Internal service token",
                    ["INTERNAL_SERVICE_TOKEN", "InternalService:Token"],
                    ["INTERNAL_SERVICE_TOKEN_FILE", "InternalService:TokenFile"]),
            ]);

        return Ok(response);
    }

    private int GetInt(string environmentKey, string configurationKey, int fallback)
    {
        string? value = _configuration[environmentKey] ?? _configuration[configurationKey];
        return int.TryParse(value, out int parsed) ? parsed : fallback;
    }

    private decimal GetDecimal(string environmentKey, string configurationKey, decimal fallback)
    {
        string? value = _configuration[environmentKey] ?? _configuration[configurationKey];
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed)
            ? parsed
            : fallback;
    }

    private SecretConfigurationStatusResponse SecretStatus(
        string name,
        IReadOnlyList<string> valueKeys,
        IReadOnlyList<string> fileKeys)
    {
        if (valueKeys.Any(valueKey => !string.IsNullOrWhiteSpace(_configuration[valueKey])))
        {
            return new SecretConfigurationStatusResponse(name, "Configured");
        }

        if (fileKeys.Any(FileKeyExists))
        {
            return new SecretConfigurationStatusResponse(name, "Configured");
        }

        return new SecretConfigurationStatusResponse(name, "Missing");
    }

    private bool FileKeyExists(string fileKey)
    {
        string? filePath = _configuration[fileKey];
        return !string.IsNullOrWhiteSpace(filePath) && System.IO.File.Exists(filePath);
    }
}
