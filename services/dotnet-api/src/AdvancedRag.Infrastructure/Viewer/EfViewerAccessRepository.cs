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

}
