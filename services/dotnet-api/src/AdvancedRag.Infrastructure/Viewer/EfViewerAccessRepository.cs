using AdvancedRag.App.Viewer;
using AdvancedRag.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdvancedRag.Infrastructure.Viewer;

public sealed class EfViewerAccessRepository : IViewerAccessRepository
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

    public async Task SaveExchangeCodeAsync(ViewerExchangeCodeRecord code, CancellationToken ct)
    {
        _db.ViewerExchangeCodes.Add(new ViewerExchangeCode
        {
            Id = code.Id,
            CodeHash = code.CodeHash,
            DocumentId = code.DocumentId,
            UserId = code.UserId,
            Purpose = code.Purpose,
            AllowedStatuses = code.AllowedStatuses,
            ExpiresAt = code.ExpiresAt,
            ConsumedAt = code.ConsumedAt,
            CreatedAt = code.CreatedAt,
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<ViewerExchangeCodeRecord?> FindExchangeCodeByHashAsync(string codeHash, CancellationToken ct)
    {
        ViewerExchangeCode? code = await _db.ViewerExchangeCodes
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.CodeHash == codeHash, ct);
        return code is null ? null : ToRecord(code);
    }

    public async Task MarkExchangeCodeConsumedAsync(Guid exchangeCodeId, DateTimeOffset consumedAt, CancellationToken ct)
    {
        ViewerExchangeCode code = await _db.ViewerExchangeCodes.SingleAsync(item => item.Id == exchangeCodeId, ct);
        code.ConsumedAt = consumedAt;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SaveTokenAuditAsync(ViewerTokenAuditRecord audit, CancellationToken ct)
    {
        _db.ViewerTokenAudit.Add(new ViewerTokenAudit
        {
            Id = audit.Id,
            ViewerTokenId = audit.ViewerTokenId,
            DocumentId = audit.DocumentId,
            UserId = audit.UserId,
            Purpose = audit.Purpose,
            IssuedAt = audit.IssuedAt,
            ExpiresAt = audit.ExpiresAt,
        });
        await _db.SaveChangesAsync(ct);
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

    private static ViewerExchangeCodeRecord ToRecord(ViewerExchangeCode code)
    {
        return new ViewerExchangeCodeRecord(
            code.Id,
            code.CodeHash,
            code.DocumentId,
            code.UserId,
            code.Purpose,
            code.AllowedStatuses,
            code.ExpiresAt,
            code.ConsumedAt,
            code.CreatedAt);
    }
}
