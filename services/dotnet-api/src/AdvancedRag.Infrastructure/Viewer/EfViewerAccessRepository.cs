using AdvancedRag.App.Viewer;
using AdvancedRag.App.Documents;
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
        IReadOnlyList<DocumentAccessRuleRecord> accessRules = await FindAccessRulesAsync(document.Id, ct);

        return new ViewerDocumentAccess(
            document.Id,
            document.Title,
            document.CurrentState,
            draft,
            published,
            accessRules);
    }

    private async Task<IReadOnlyList<DocumentAccessRuleRecord>> FindAccessRulesAsync(
        Guid documentId,
        CancellationToken ct)
    {
        DocumentPermission[] permissions = await _db.DocumentPermissions
            .AsNoTracking()
            .Where(permission => permission.DocumentId == documentId)
            .OrderBy(permission => permission.Id)
            .ToArrayAsync(ct);
        Guid[] permissionIds = permissions.Select(permission => permission.Id).ToArray();
        DocumentPermissionGroup[] permissionGroups = await _db.DocumentPermissionGroups
            .AsNoTracking()
            .Where(group => permissionIds.Contains(group.DocumentPermissionId))
            .ToArrayAsync(ct);
        ILookup<Guid, Guid> groupsByPermissionId = permissionGroups.ToLookup(
            group => group.DocumentPermissionId,
            group => group.GroupId);

        return permissions
            .Select(permission =>
            {
                Guid[] groupIds = groupsByPermissionId[permission.Id]
                    .Concat(permission.GroupId is null ? [] : [permission.GroupId.Value])
                    .Distinct()
                    .Order()
                    .ToArray();
                return new DocumentAccessRuleRecord(
                    permission.Id,
                    permission.OrganizationalUnitId,
                    groupIds);
            })
            .Where(rule => rule.OrganizationalUnitId is not null || rule.GroupIds.Count > 0)
            .ToArray();
    }

    private async Task<ViewerDocumentVersion?> FindVersionAsync(Guid versionId, CancellationToken ct)
    {
        DocumentVersion? version = await _db.DocumentVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == versionId, ct);
        if (version is null)
        {
            return null;
        }

        string documentType = version.DocumentTypeId is null
            ? string.Empty
            : await _db.DocumentTypes
                .AsNoTracking()
                .Where(type => type.Id == version.DocumentTypeId.Value)
                .Select(type => type.Name)
                .SingleOrDefaultAsync(ct) ?? string.Empty;

        return new ViewerDocumentVersion(
            version.Id,
            version.VersionNumber,
            version.State,
            version.Title,
            documentType,
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
