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
        var instructions = await _db.Instructions
            .AsNoTracking()
            .OrderByDescending(instruction => instruction.UpdatedAt)
            .ToListAsync(ct);

        var aggregates = new List<DocumentSummary>(instructions.Count);
        foreach (var instruction in instructions)
        {
            var aggregate = await BuildAggregateAsync(instruction, ct);
            aggregates.Add(DocumentSummary.FromAggregate(aggregate));
        }

        return aggregates;
    }

    public async Task<DocumentAggregate?> FindAsync(Guid instructionId, CancellationToken ct)
    {
        var instruction = await _db.Instructions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == instructionId, ct);

        return instruction is null ? null : await BuildAggregateAsync(instruction, ct);
    }

    public async Task SaveAsync(
        DocumentAggregate document,
        IReadOnlyList<ReviewCommentRecord> comments,
        IReadOnlyList<DocumentAuditEvent> auditEvents,
        CancellationToken ct)
    {
        var existing = await _db.Instructions.SingleOrDefaultAsync(item => item.Id == document.Id, ct);
        if (existing is null)
        {
            _db.Instructions.Add(new Instruction
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

        await _db.InstructionPermissions
            .Where(permission => permission.InstructionId == document.Id)
            .ExecuteDeleteAsync(ct);
        _db.InstructionPermissions.AddRange(document.AllowedGroupIds.Select(groupId => new InstructionPermission
        {
            Id = Guid.NewGuid(),
            InstructionId = document.Id,
            GroupId = groupId,
            CreatedAt = DateTimeOffset.UtcNow,
        }));

        _db.ReviewComments.AddRange(comments.Select(comment => new ReviewComment
        {
            Id = Guid.NewGuid(),
            InstructionVersionId = comment.InstructionVersionId,
            ActorUserId = comment.ActorUserId,
            Comment = comment.Comment,
            CreatedAt = DateTimeOffset.UtcNow,
        }));

        _db.AuditEvents.AddRange(auditEvents.Select(audit => new AuditEvent
        {
            Id = Guid.NewGuid(),
            ActorUserId = audit.ActorUserId,
            EventType = audit.EventType,
            EntityType = "instruction",
            EntityId = audit.EntityId,
            DetailsJson = JsonSerializer.Serialize(audit.Details),
            RequestId = audit.RequestId,
            CreatedAt = DateTimeOffset.UtcNow,
        }));

        await _db.SaveChangesAsync(ct);
    }

    private async Task<DocumentAggregate> BuildAggregateAsync(Instruction instruction, CancellationToken ct)
    {
        DocumentVersionRecord? draft = null;
        DocumentVersionRecord? published = null;

        if (instruction.CurrentDraftVersionId is not null)
        {
            var version = await _db.InstructionVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == instruction.CurrentDraftVersionId, ct);
            draft = version is null ? null : ToRecord(version);
        }

        if (instruction.CurrentPublishedVersionId is not null)
        {
            var version = await _db.InstructionVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == instruction.CurrentPublishedVersionId, ct);
            published = version is null ? null : ToRecord(version);
        }

        var groupIds = await _db.InstructionPermissions
            .AsNoTracking()
            .Where(permission => permission.InstructionId == instruction.Id && permission.GroupId != null)
            .Select(permission => permission.GroupId!.Value)
            .Order()
            .ToArrayAsync(ct);

        return new DocumentAggregate(
            instruction.Id,
            instruction.Title,
            ParseEnum<InstructionState>(instruction.CurrentState),
            draft,
            published,
            groupIds,
            instruction.CreatedByUserId,
            instruction.CreatedAt,
            instruction.UpdatedAt);
    }

    private async Task UpsertVersionAsync(DocumentVersionRecord version, CancellationToken ct)
    {
        var existing = await _db.InstructionVersions.SingleOrDefaultAsync(item => item.Id == version.Id, ct);
        if (existing is null)
        {
            _db.InstructionVersions.Add(new InstructionVersion
            {
                Id = version.Id,
                InstructionId = version.InstructionId,
                VersionNumber = version.VersionNumber,
                State = ToStorage(version.State),
                Title = version.Title,
                InstructionType = version.InstructionType,
                Audience = version.Audience,
                ContentHtml = version.ContentHtml,
                CreatedAt = version.CreatedAt,
                SubmittedForReviewAt = version.SubmittedForReviewAt,
                SubmittedForReviewByUserId = version.SubmittedForReviewByUserId,
                PublishedAt = version.PublishedAt,
                PublishedByUserId = version.PublishedByUserId,
                IndexingStatus = ToStorage(version.IndexingStatus),
            });
            return;
        }

        existing.State = ToStorage(version.State);
        existing.Title = version.Title;
        existing.InstructionType = version.InstructionType;
        existing.Audience = version.Audience;
        existing.ContentHtml = version.ContentHtml;
        existing.SubmittedForReviewAt = version.SubmittedForReviewAt;
        existing.SubmittedForReviewByUserId = version.SubmittedForReviewByUserId;
        existing.PublishedAt = version.PublishedAt;
        existing.PublishedByUserId = version.PublishedByUserId;
        existing.IndexingStatus = ToStorage(version.IndexingStatus);
    }

    private static DocumentVersionRecord ToRecord(InstructionVersion version)
    {
        return new DocumentVersionRecord(
            version.Id,
            version.InstructionId,
            version.VersionNumber,
            ParseEnum<InstructionVersionState>(version.State),
            version.Title,
            version.InstructionType,
            version.Audience,
            version.ContentHtml,
            version.CreatedAt,
            version.SubmittedForReviewAt,
            version.SubmittedForReviewByUserId,
            version.PublishedAt,
            version.PublishedByUserId,
            ParseEnum<IndexingStatus>(version.IndexingStatus));
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
            InstructionState.InReview => "In Review",
            InstructionVersionState.InReview => "In Review",
            _ => value.ToString(),
        };
    }
}
