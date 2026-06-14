using System.Text.Json;
using AdvancedRag.App.Documents;
using AdvancedRag.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdvancedRag.Infrastructure.Documents;

public sealed class EfDocumentRepository : IDocumentRepository
{
    private readonly AppDbContext _db;

    public EfDocumentRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<DocumentSummary>> ListAsync(CancellationToken ct)
    {
        var documents = await _db.Documents
            .AsNoTracking()
            .OrderByDescending(document => document.UpdatedAt)
            .ToListAsync(ct);

        var aggregates = new List<DocumentSummary>(documents.Count);
        foreach (var document in documents)
        {
            var aggregate = await BuildAggregateAsync(document, ct);
            aggregates.Add(DocumentSummary.FromAggregate(aggregate));
        }

        return aggregates;
    }

    public async Task<DocumentAggregate?> FindAsync(Guid documentId, CancellationToken ct)
    {
        var document = await _db.Documents
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == documentId, ct);

        return document is null ? null : await BuildAggregateAsync(document, ct);
    }

    public async Task SaveAsync(
        DocumentAggregate document,
        IReadOnlyList<ReviewCommentRecord> comments,
        IReadOnlyList<DocumentAuditEvent> auditEvents,
        CancellationToken ct)
    {
        var existing = await _db.Documents.SingleOrDefaultAsync(item => item.Id == document.Id, ct);
        if (existing is null)
        {
            _db.Documents.Add(new Document
            {
                Id = document.Id,
                Title = document.Title,
                CurrentState = ToStorage(document.State),
                CurrentDraftVersionId = document.CurrentDraftVersion?.Id,
                CurrentPublishedVersionId = document.CurrentPublishedVersion?.Id,
                CreatedByUserId = document.CreatedByUserId,
                CreatedAt = document.CreatedAt,
                UpdatedAt = document.UpdatedAt,
            });
        }
        else
        {
            existing.Title = document.Title;
            existing.CurrentState = ToStorage(document.State);
            existing.CurrentDraftVersionId = document.CurrentDraftVersion?.Id;
            existing.CurrentPublishedVersionId = document.CurrentPublishedVersion?.Id;
            existing.UpdatedAt = document.UpdatedAt;
        }

        if (document.CurrentDraftVersion is not null)
        {
            await UpsertVersionAsync(document.CurrentDraftVersion, ct);
        }

        if (document.CurrentPublishedVersion is not null)
        {
            await UpsertVersionAsync(document.CurrentPublishedVersion, ct);
        }

        Guid[] existingPermissionIds = await _db.DocumentPermissions
            .Where(permission => permission.DocumentId == document.Id)
            .Select(permission => permission.Id)
            .ToArrayAsync(ct);
        await _db.DocumentPermissionGroups
            .Where(group => existingPermissionIds.Contains(group.DocumentPermissionId))
            .ExecuteDeleteAsync(ct);
        await _db.DocumentPermissions
            .Where(permission => permission.DocumentId == document.Id)
            .ExecuteDeleteAsync(ct);

        // ExecuteDelete issues an immediate SQL DELETE but does not evict already-tracked
        // entities from the change tracker. Publishing saves twice on one scoped DbContext
        // (Pending before indexing, Published after), so the DocumentPermission/Group rows
        // added by the first save are still tracked under these ids; re-adding them below
        // would throw an identity-map conflict (surfaced as HTTP 500). Detach any lingering
        // permission entities so the rewrite stays safe across repeated saves in one request.
        foreach (var trackedPermission in _db.ChangeTracker.Entries<DocumentPermission>().ToList())
        {
            trackedPermission.State = EntityState.Detached;
        }

        foreach (var trackedPermissionGroup in _db.ChangeTracker.Entries<DocumentPermissionGroup>().ToList())
        {
            trackedPermissionGroup.State = EntityState.Detached;
        }

        foreach (DocumentAccessRuleRecord rule in document.AccessRules)
        {
            _db.DocumentPermissions.Add(new DocumentPermission
            {
                Id = rule.Id,
                DocumentId = document.Id,
                OrganizationalUnitId = rule.OrganizationalUnitId,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            _db.DocumentPermissionGroups.AddRange(rule.GroupIds.Select(groupId => new DocumentPermissionGroup
            {
                DocumentPermissionId = rule.Id,
                GroupId = groupId,
            }));
        }

        _db.ReviewComments.AddRange(comments.Select(comment => new ReviewComment
        {
            Id = Guid.NewGuid(),
            DocumentVersionId = comment.DocumentVersionId,
            ActorUserId = comment.ActorUserId,
            Comment = comment.Comment,
            CreatedAt = DateTimeOffset.UtcNow,
        }));

        _db.AuditEvents.AddRange(auditEvents.Select(audit => new AuditEvent
        {
            Id = Guid.NewGuid(),
            ActorUserId = audit.ActorUserId,
            EventType = audit.EventType,
            EntityType = "document",
            EntityId = audit.EntityId,
            DetailsJson = JsonSerializer.Serialize(audit.Details),
            RequestId = audit.RequestId,
            CreatedAt = DateTimeOffset.UtcNow,
        }));

        await _db.SaveChangesAsync(ct);
    }

    private async Task<DocumentAggregate> BuildAggregateAsync(Document document, CancellationToken ct)
    {
        DocumentVersionRecord? draft = null;
        DocumentVersionRecord? published = null;

        if (document.CurrentDraftVersionId is not null)
        {
            var version = await _db.DocumentVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == document.CurrentDraftVersionId, ct);
            draft = version is null ? null : ToRecord(version, await ResolveTypeNameAsync(version.DocumentTypeId, ct));
        }

        if (document.CurrentPublishedVersionId is not null)
        {
            var version = await _db.DocumentVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == document.CurrentPublishedVersionId, ct);
            published = version is null ? null : ToRecord(version, await ResolveTypeNameAsync(version.DocumentTypeId, ct));
        }

        DocumentPermission[] permissions = await _db.DocumentPermissions
            .AsNoTracking()
            .Where(permission => permission.DocumentId == document.Id)
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
        DocumentAccessRuleRecord[] accessRules = permissions
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

        return new DocumentAggregate(
            document.Id,
            document.Title,
            ParseEnum<DocumentState>(document.CurrentState),
            draft,
            published,
            accessRules,
            document.CreatedByUserId,
            document.CreatedAt,
            document.UpdatedAt);
    }

    private async Task UpsertVersionAsync(DocumentVersionRecord version, CancellationToken ct)
    {
        var existing = await _db.DocumentVersions.SingleOrDefaultAsync(item => item.Id == version.Id, ct);
        if (existing is null)
        {
            _db.DocumentVersions.Add(new DocumentVersion
            {
                Id = version.Id,
                DocumentId = version.DocumentId,
                VersionNumber = version.VersionNumber,
                State = ToStorage(version.State),
                Title = version.Title,
                DocumentTypeId = version.DocumentTypeId,
                Audience = version.Audience,
                ContentHtml = version.ContentHtml,
                CreatedAt = version.CreatedAt,
                SubmittedForReviewAt = version.SubmittedForReviewAt,
                SubmittedForReviewByUserId = version.SubmittedForReviewByUserId,
                PublishedAt = version.PublishedAt,
                PublishedByUserId = version.PublishedByUserId,
                IndexingJobId = version.IndexingJobId,
                IndexingStatus = ToStorage(version.IndexingStatus),
            });
            return;
        }

        existing.State = ToStorage(version.State);
        existing.Title = version.Title;
        existing.DocumentTypeId = version.DocumentTypeId;
        existing.Audience = version.Audience;
        existing.ContentHtml = version.ContentHtml;
        existing.SubmittedForReviewAt = version.SubmittedForReviewAt;
        existing.SubmittedForReviewByUserId = version.SubmittedForReviewByUserId;
        existing.PublishedAt = version.PublishedAt;
        existing.PublishedByUserId = version.PublishedByUserId;
        existing.IndexingJobId = version.IndexingJobId;
        existing.IndexingStatus = ToStorage(version.IndexingStatus);
    }

    private async Task<string> ResolveTypeNameAsync(Guid? documentTypeId, CancellationToken ct)
    {
        if (documentTypeId is null)
        {
            return string.Empty;
        }

        return await _db.DocumentTypes
            .AsNoTracking()
            .Where(type => type.Id == documentTypeId.Value)
            .Select(type => type.Name)
            .SingleOrDefaultAsync(ct) ?? string.Empty;
    }

    private static DocumentVersionRecord ToRecord(DocumentVersion version, string documentTypeName)
    {
        return new DocumentVersionRecord(
            version.Id,
            version.DocumentId,
            version.VersionNumber,
            ParseEnum<DocumentVersionState>(version.State),
            version.Title,
            version.DocumentTypeId,
            documentTypeName,
            version.Audience,
            version.ContentHtml,
            version.CreatedAt,
            version.SubmittedForReviewAt,
            version.SubmittedForReviewByUserId,
            version.PublishedAt,
            version.PublishedByUserId,
            ParseEnum<IndexingStatus>(version.IndexingStatus),
            version.IndexingJobId);
    }

    private static TEnum ParseEnum<TEnum>(string value)
        where TEnum : struct
    {
        return Enum.TryParse<TEnum>(value.Replace(" ", string.Empty, StringComparison.Ordinal), out var parsed)
            ? parsed
            : default;
    }

    private static string ToStorage<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        return value switch
        {
            DocumentState.InReview => "In Review",
            DocumentVersionState.InReview => "In Review",
            _ => value.ToString(),
        };
    }
}
