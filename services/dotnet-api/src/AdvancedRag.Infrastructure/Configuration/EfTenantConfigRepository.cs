using AdvancedRag.App.Configuration;
using AdvancedRag.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using TenantConfigApp = AdvancedRag.App.Configuration.TenantConfig;
using TenantConfigEntity = AdvancedRag.Infrastructure.Persistence.TenantConfig;

namespace AdvancedRag.Infrastructure.Configuration;

public sealed class EfTenantConfigRepository : ITenantConfigRepository
{
    private readonly AppDbContext _db;

    public EfTenantConfigRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<TenantConfigApp> GetAsync(CancellationToken ct)
    {
        TenantConfigEntity? entity = await _db.TenantConfigs.AsNoTracking().SingleOrDefaultAsync(ct);
        if (entity is not null)
        {
            return ToApp(entity);
        }

        return await UpsertAsync(TenantConfigDraft.CreateDefault(), ct);
    }

    public async Task<TenantConfigApp> UpsertAsync(TenantConfigDraft draft, CancellationToken ct)
    {
        TenantConfigEntity? entity = await _db.TenantConfigs.SingleOrDefaultAsync(ct);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (entity is null)
        {
            entity = new TenantConfigEntity
            {
                Id = Guid.NewGuid(),
                CreatedAt = now,
                BrandName = draft.BrandName,
                PrimaryColor = draft.PrimaryColor,
                DefaultLocale = draft.DefaultLocale,
                SupportedLocales = draft.SupportedLocales.ToArray(),
                LlmProvider = draft.LlmProvider,
                LlmModel = draft.LlmModel,
                EmbeddingProvider = draft.EmbeddingProvider,
                EmbeddingModel = draft.EmbeddingModel,
                RerankerProvider = draft.RerankerProvider,
                RerankerModel = draft.RerankerModel,
                S3Bucket = draft.S3Bucket,
                S3Region = draft.S3Region,
            };
            _db.TenantConfigs.Add(entity);
        }

        entity.BrandName = draft.BrandName;
        entity.BrandLogoUrl = draft.BrandLogoUrl;
        entity.BrandFaviconUrl = draft.BrandFaviconUrl;
        entity.PrimaryColor = draft.PrimaryColor;
        entity.DefaultLocale = draft.DefaultLocale;
        entity.SupportedLocales = draft.SupportedLocales.ToArray();
        entity.LlmProvider = draft.LlmProvider;
        entity.LlmModel = draft.LlmModel;
        entity.LlmBaseUrl = draft.LlmBaseUrl;
        entity.EmbeddingProvider = draft.EmbeddingProvider;
        entity.EmbeddingModel = draft.EmbeddingModel;
        entity.EmbeddingDimensions = draft.EmbeddingDimensions;
        entity.RerankerProvider = draft.RerankerProvider;
        entity.RerankerModel = draft.RerankerModel;
        entity.RerankerBaseUrl = draft.RerankerBaseUrl;
        entity.EnableBm25 = draft.EnableBm25;
        entity.EnableReranker = draft.EnableReranker;
        entity.EnableConversationalMemory = draft.EnableConversationalMemory;
        entity.EnableQueryRewrite = draft.EnableQueryRewrite;
        entity.RagTopKVector = draft.RagTopKVector;
        entity.RagTopKBm25 = draft.RagTopKBm25;
        entity.RagTopKFinal = draft.RagTopKFinal;
        entity.RrfK = draft.RrfK;
        entity.ConversationHistoryTurns = draft.ConversationHistoryTurns;
        entity.CacheTtlHours = draft.CacheTtlHours;
        entity.CacheSimilarityThreshold = draft.CacheSimilarityThreshold;
        entity.DefaultMonthlyBudgetUsd = draft.DefaultMonthlyBudgetUsd;
        entity.GlobalDailyBudgetUsd = draft.GlobalDailyBudgetUsd;
        entity.EnableVlmImageDescription = draft.EnableVlmImageDescription;
        entity.EnableOtel = draft.EnableOtel;
        entity.S3Endpoint = draft.S3Endpoint;
        entity.S3Bucket = draft.S3Bucket;
        entity.S3Region = draft.S3Region;
        entity.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);
        return ToApp(entity);
    }

    private static TenantConfigApp ToApp(TenantConfigEntity entity)
    {
        return new TenantConfigApp(
            entity.Id,
            entity.BrandName,
            entity.BrandLogoUrl,
            entity.BrandFaviconUrl,
            entity.PrimaryColor,
            entity.DefaultLocale,
            entity.SupportedLocales,
            entity.LlmProvider,
            entity.LlmModel,
            entity.LlmBaseUrl,
            entity.EmbeddingProvider,
            entity.EmbeddingModel,
            entity.EmbeddingDimensions,
            entity.RerankerProvider,
            entity.RerankerModel,
            entity.RerankerBaseUrl,
            entity.EnableBm25,
            entity.EnableReranker,
            entity.EnableConversationalMemory,
            entity.EnableQueryRewrite,
            entity.RagTopKVector,
            entity.RagTopKBm25,
            entity.RagTopKFinal,
            entity.RrfK,
            entity.ConversationHistoryTurns,
            entity.CacheTtlHours,
            entity.CacheSimilarityThreshold,
            entity.DefaultMonthlyBudgetUsd,
            entity.GlobalDailyBudgetUsd,
            entity.EnableVlmImageDescription,
            entity.EnableOtel,
            entity.S3Endpoint,
            entity.S3Bucket,
            entity.S3Region,
            entity.CreatedAt,
            entity.UpdatedAt);
    }
}
