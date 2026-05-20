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

        await _db.DocumentPermissions
            .Where(permission => permission.DocumentId == document.Id)
            .ExecuteDeleteAsync(ct);
        _db.DocumentPermissions.AddRange(document.AllowedGroupIds.Select(groupId => new DocumentPermission
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            GroupId = groupId,
            CreatedAt = DateTimeOffset.UtcNow,
        }));

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
            draft = version is null ? null : ToRecord(version);
        }

        if (document.CurrentPublishedVersionId is not null)
        {
            var version = await _db.DocumentVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == document.CurrentPublishedVersionId, ct);
            published = version is null ? null : ToRecord(version);
        }

        var groupIds = await _db.DocumentPermissions
            .AsNoTracking()
            .Where(permission => permission.DocumentId == document.Id && permission.GroupId != null)
            .OrderBy(permission => permission.GroupId)
            .Select(permission => permission.GroupId!.Value)
            .ToArrayAsync(ct);

        return new DocumentAggregate(
            document.Id,
            document.Title,
            ParseEnum<DocumentState>(document.CurrentState),
            draft,
            published,
            groupIds,
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
                DocumentType = version.DocumentType,
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
        existing.DocumentType = version.DocumentType;
        existing.Audience = version.Audience;
        existing.ContentHtml = version.ContentHtml;
        existing.SubmittedForReviewAt = version.SubmittedForReviewAt;
        existing.SubmittedForReviewByUserId = version.SubmittedForReviewByUserId;
        existing.PublishedAt = version.PublishedAt;
        existing.PublishedByUserId = version.PublishedByUserId;
        existing.IndexingJobId = version.IndexingJobId;
        existing.IndexingStatus = ToStorage(version.IndexingStatus);
    }

    private static DocumentVersionRecord ToRecord(DocumentVersion version)
    {
        return new DocumentVersionRecord(
            version.Id,
            version.DocumentId,
            version.VersionNumber,
            ParseEnum<DocumentVersionState>(version.State),
            version.Title,
            version.DocumentType,
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
