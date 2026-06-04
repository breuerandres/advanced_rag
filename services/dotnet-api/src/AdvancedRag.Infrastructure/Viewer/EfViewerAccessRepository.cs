using AdvancedRag.App.Viewer;
using AdvancedRag.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdvancedRag.Infrastructure.Viewer;

public sealed class EfViewerAccessRepository : IViewerAccessRepository, IViewerSessionHandoffRepository
{
    private readonly AppDbContext _db;

    public EfViewerAccessRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ViewerDocumentAccess?> FindDocumentAsync(Guid documentId, CancellationToken ct)
    {
        Document? document = await _db.Documents
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == documentId, ct);

        if (document is null)
        {
            return null;
        }

        ViewerDocumentVersion? draft = document.CurrentDraftVersionId is null
            ? null
            : await FindVersionAsync(document.CurrentDraftVersionId.Value, ct);
        ViewerDocumentVersion? published = document.CurrentPublishedVersionId is null
            ? null
            : await FindVersionAsync(document.CurrentPublishedVersionId.Value, ct);

        return new ViewerDocumentAccess(
            document.Id,
            document.Title,
            document.CurrentState,
            draft,
            published);
    }

    private async Task<ViewerDocumentVersion?> FindVersionAsync(Guid versionId, CancellationToken ct)
    {
        DocumentVersion? version = await _db.DocumentVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == versionId, ct);
        return version is null
            ? null
            : new ViewerDocumentVersion(
                version.Id,
                version.VersionNumber,
                version.State,
                version.Title,
                version.DocumentType,
                version.Audience,
                version.ContentHtml);
    }

    public async Task StoreAsync(ViewerSessionHandoffRecord record, CancellationToken ct)
    {
        _db.ViewerSessionHandoffCodes.Add(new ViewerSessionHandoffCode
        {
            Id = record.Id,
            CodeHash = record.CodeHash,
            UserId = record.UserId,
            DocumentId = record.DocumentId,
            Purpose = record.Purpose,
            AllowedStateScope = record.AllowedStateScope,
            ExpiresAt = record.ExpiresAt,
            ConsumedAt = record.ConsumedAt,
            CreatedAt = record.CreatedAt,
            RequestId = record.RequestId,
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<ViewerSessionHandoffRecord?> FindByCodeHashAsync(string codeHash, CancellationToken ct)
    {
        ViewerSessionHandoffCode? record = await _db.ViewerSessionHandoffCodes
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.CodeHash == codeHash, ct);
        return record is null
            ? null
            : new ViewerSessionHandoffRecord(
                record.Id,
                record.CodeHash,
                record.UserId,
                record.DocumentId,
                record.Purpose,
                record.AllowedStateScope,
                record.ExpiresAt,
                record.ConsumedAt,
                record.CreatedAt,
                record.RequestId);
    }

    public async Task MarkConsumedAsync(Guid id, DateTimeOffset consumedAt, CancellationToken ct)
    {
        await _db.ViewerSessionHandoffCodes
            .Where(item => item.Id == id && item.ConsumedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.ConsumedAt, consumedAt),
                ct);
    }
}
