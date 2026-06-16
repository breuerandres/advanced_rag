using AdvancedRag.Api.Errors;
using AdvancedRag.Api.Middleware;
using AdvancedRag.Api.Models.Configuration;
using AdvancedRag.App.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdvancedRag.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/configuration")]
public sealed class ConfigurationController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ITenantConfigService _tenantConfig;

    public ConfigurationController(IConfiguration configuration, ITenantConfigService tenantConfig)
    {
        _configuration = configuration;
        _tenantConfig = tenantConfig;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        TenantConfig config = await _tenantConfig.GetAsync(ct);
        return Ok(BuildResponse(config));
    }

    private ConfigurationResponse BuildResponse(TenantConfig config)
    {
        return new ConfigurationResponse(
            CustomerTimezone: config.CustomerTimezone,
            LlmProvider: config.LlmProvider,
            ChatModel: config.LlmModel,
            EmbeddingModel: config.EmbeddingModel,
            EmbeddingDimensions: config.EmbeddingDimensions,
            DefaultMonthlyAiBudgetUsd: config.DefaultMonthlyBudgetUsd,
            SemanticCacheTtlHours: config.CacheTtlHours,
            SemanticCacheSimilarityThreshold: config.CacheSimilarityThreshold,
            ChatMaxQuestionChars: config.ChatMaxQuestionChars,
            ImportMaxFileSizeMb: config.ImportMaxFileSizeMb,
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
                    ["INTERNAL_SERVICE_TOKEN_FILE", "InternalService:TokenFile", "InternalServiceTokenFile"]),
                SecretStatus(
                    "S3 access key",
                    ["S3_ACCESS_KEY", "S3:AccessKey"],
                    ["S3_ACCESS_KEY_FILE", "S3:AccessKeyFile"]),
                SecretStatus(
                    "S3 secret key",
                    ["S3_SECRET_KEY", "S3:SecretKey"],
                    ["S3_SECRET_KEY_FILE", "S3:SecretKeyFile"]),
            ]);
    }

    [HttpPut]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update([FromBody] UpdateConfigurationRequest request, CancellationToken ct)
    {
        TenantConfig current = await _tenantConfig.GetAsync(ct);
        TenantConfigDraft draft = new(
            current.BrandName, current.BrandLogoUrl, current.BrandFaviconUrl, current.PrimaryColor,
            current.DefaultLocale, current.SupportedLocales, current.LlmProvider,
            request.ChatModel, current.LlmBaseUrl, current.EmbeddingProvider, current.EmbeddingModel,
            current.EmbeddingDimensions, current.RerankerProvider, current.RerankerModel,
            current.RerankerBaseUrl, current.EnableBm25, current.EnableReranker,
            current.EnableConversationalMemory, current.EnableQueryRewrite, current.RagTopKVector,
            current.RagTopKBm25, current.RagTopKFinal, current.RrfK, current.ConversationHistoryTurns,
            request.SemanticCacheTtlHours, request.SemanticCacheSimilarityThreshold,
            request.DefaultMonthlyAiBudgetUsd, current.GlobalDailyBudgetUsd,
            current.EnableVlmImageDescription, current.EnableOtel, current.S3Endpoint,
            current.S3Bucket, current.S3Region, request.CustomerTimezone,
            request.ImportMaxFileSizeMb, request.ChatMaxQuestionChars, current.SeededFromEnv);

        try
        {
            TenantConfig updated = await _tenantConfig.UpdateAsync(draft, ct);
            return Ok(BuildResponse(updated));
        }
        catch (TenantConfigException exception)
        {
            return StatusCode(
                exception.HttpStatus,
                ErrorResponse.Create(
                    exception.Code,
                    exception.Message,
                    HttpContext.Items.TryGetValue(RequestIdMiddleware.ContextItemKey, out object? value)
                        ? value?.ToString() ?? string.Empty
                        : string.Empty,
                    exception.Details));
        }
    }

    [HttpGet("/api/v1/config")]
    [AllowAnonymous]
    public async Task<ActionResult<TenantConfigResponse>> GetTenantConfigAsync(CancellationToken ct)
    {
        TenantConfig config = await _tenantConfig.GetAsync(ct);
        return Ok(TenantConfigResponse.FromTenantConfig(config));
    }

    [HttpPut("/api/v1/config")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateTenantConfigAsync(
        [FromBody] UpdateTenantConfigRequest request,
        CancellationToken ct)
    {
        try
        {
            TenantConfig config = await _tenantConfig.UpdateAsync(request.ToDraft(), ct);
            return Ok(TenantConfigResponse.FromTenantConfig(config));
        }
        catch (TenantConfigException exception)
        {
            return StatusCode(
                exception.HttpStatus,
                ErrorResponse.Create(
                    exception.Code,
                    exception.Message,
                    HttpContext.Items.TryGetValue(RequestIdMiddleware.ContextItemKey, out object? value)
                        ? value?.ToString() ?? string.Empty
                        : string.Empty,
                    exception.Details));
        }
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
