using System.Text.RegularExpressions;

namespace AdvancedRag.App.Documents;

public interface IDocumentLifecycleService
{
    Task<IReadOnlyList<DocumentSummary>> ListAsync(CancellationToken ct);

    Task<DocumentAggregate> CreateDraftAsync(CreateDocumentCommand command, CancellationToken ct);

    Task<DocumentAggregate?> GetAsync(Guid instructionId, CancellationToken ct);

    Task<DocumentAggregate> UpdateDraftAsync(UpdateDraftCommand command, CancellationToken ct);

    Task<DocumentAggregate> SendToReviewAsync(SendToReviewCommand command, CancellationToken ct);

    Task<DocumentAggregate> ReturnToDraftAsync(ReturnToDraftCommand command, CancellationToken ct);

    Task<DocumentAggregate> RequestPublishAsync(RequestPublishCommand command, CancellationToken ct);

    Task<DocumentAggregate> ArchiveAsync(ArchiveInstructionCommand command, CancellationToken ct);

    Task<DocumentAggregate> RestoreAsync(RestoreInstructionCommand command, CancellationToken ct);
}

public sealed class DocumentLifecycleService : IDocumentLifecycleService
{
    private static readonly Regex HtmlTagPattern = new("<[^>]+>", RegexOptions.Compiled);

    private readonly IDocumentRepository _repository;
    private readonly IInternalIndexingClient _indexingClient;
    private readonly IInstructionHtmlSanitizer _htmlSanitizer;

    public DocumentLifecycleService(
        IDocumentRepository repository,
        IInstructionHtmlSanitizer? htmlSanitizer = null,
        IInternalIndexingClient? indexingClient = null)
    {
        _repository = repository;
        _indexingClient = indexingClient ?? new UnavailableInternalIndexingClient();
        _htmlSanitizer = htmlSanitizer ?? new PassthroughInstructionHtmlSanitizer();
    }

    public Task<IReadOnlyList<DocumentSummary>> ListAsync(CancellationToken ct)
    {
        return _repository.ListAsync(ct);
    }

    public Task<DocumentAggregate?> GetAsync(Guid instructionId, CancellationToken ct)
    {
        return _repository.FindAsync(instructionId, ct);
    }

    public async Task<DocumentAggregate> CreateDraftAsync(CreateDocumentCommand command, CancellationToken ct)
    {
        var document = DocumentAggregate.NewDraft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            command.Title.Trim(),
            command.InstructionType.Trim(),
            command.Audience.Trim(),
            _htmlSanitizer.Sanitize(command.ContentHtml.Trim()),
            NormalizeGroupIds(command.AllowedGroupIds),
            command.ActorUserId);

        await _repository.SaveAsync(
            document,
            [],
            [Audit(command.ActorUserId, "instruction.created", document.Id, command.RequestId)],
            ct);

        return document;
    }

    public async Task<DocumentAggregate> UpdateDraftAsync(UpdateDraftCommand command, CancellationToken ct)
    {
        var document = await RequireDocumentAsync(command.InstructionId, ct);
        if (document.State == InstructionState.Archived)
        {
            throw new DocumentLifecycleException(
                "INVALID_LIFECYCLE_TRANSITION",
                409,
                "Archived instructions must be restored before editing.");
        }

        var normalizedTitle = command.Title.Trim();
        var normalizedType = command.InstructionType.Trim();
        var normalizedAudience = command.Audience.Trim();
        var normalizedContent = _htmlSanitizer.Sanitize(command.ContentHtml.Trim());
        var now = DateTimeOffset.UtcNow;
        var existingDraft = document.CurrentDraftVersion;

        DocumentVersionRecord draft;
        if (existingDraft is null)
        {
            var nextVersionNumber = (document.CurrentPublishedVersion?.VersionNumber ?? 0) + 1;
            draft = new DocumentVersionRecord(
                Guid.NewGuid(),
                document.Id,
                nextVersionNumber,
                InstructionVersionState.Draft,
                normalizedTitle,
                normalizedType,
                normalizedAudience,
                normalizedContent,
                now,
                null,
                null,
                null,
                null,
                IndexingStatus.None,
                null);
        }
        else
        {
            if (existingDraft.State != InstructionVersionState.Draft)
            {
                throw new DocumentLifecycleException(
                    "INVALID_LIFECYCLE_TRANSITION",
                    409,
                    "Only draft versions can be edited.");
            }

            draft = existingDraft with
            {
                Title = normalizedTitle,
                InstructionType = normalizedType,
                Audience = normalizedAudience,
                ContentHtml = normalizedContent,
                IndexingStatus = IndexingStatus.None,
                IndexingJobId = null,
            };
        }

        var updated = document with
        {
            Title = normalizedTitle,
            State = InstructionState.Draft,
            CurrentDraftVersion = draft,
            AllowedGroupIds = NormalizeGroupIds(command.AllowedGroupIds),
            UpdatedAt = now,
        };

        await _repository.SaveAsync(
            updated,
            [],
            [Audit(command.ActorUserId, "instruction.draft_updated", document.Id, command.RequestId)],
            ct);

        return updated;
    }

    public async Task<DocumentAggregate> SendToReviewAsync(SendToReviewCommand command, CancellationToken ct)
    {
        var document = await RequireDocumentAsync(command.InstructionId, ct);
        var draft = RequireDraft(document);
        RequireReadyForReview(document, draft);

        var now = DateTimeOffset.UtcNow;
        var updatedDraft = draft with
        {
            State = InstructionVersionState.InReview,
            SubmittedForReviewAt = now,
            SubmittedForReviewByUserId = command.ActorUserId,
        };
        var updated = document with
        {
            State = InstructionState.InReview,
            CurrentDraftVersion = updatedDraft,
            UpdatedAt = now,
        };
        var comments = string.IsNullOrWhiteSpace(command.Comment)
            ? Array.Empty<ReviewCommentRecord>()
            : [new ReviewCommentRecord(draft.Id, command.ActorUserId, command.Comment.Trim())];

        await _repository.SaveAsync(
            updated,
            comments,
            [Audit(command.ActorUserId, "instruction.send_to_review", document.Id, command.RequestId)],
            ct);

        return updated;
    }

    public async Task<DocumentAggregate> ReturnToDraftAsync(ReturnToDraftCommand command, CancellationToken ct)
    {
        var comment = RequireComment(command.Comment);
        var document = await RequireDocumentAsync(command.InstructionId, ct);
        var draft = document.CurrentDraftVersion;
        if (document.State != InstructionState.InReview
            || draft is null
            || draft.State != InstructionVersionState.InReview)
        {
            throw new DocumentLifecycleException(
                "INVALID_LIFECYCLE_TRANSITION",
                409,
                "Only in-review drafts can be returned to draft.");
        }

        var updatedDraft = draft with
        {
            State = InstructionVersionState.Draft,
            SubmittedForReviewAt = null,
            SubmittedForReviewByUserId = null,
            IndexingStatus = IndexingStatus.None,
        };
        var updated = document with
        {
            State = InstructionState.Draft,
            CurrentDraftVersion = updatedDraft,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await _repository.SaveAsync(
            updated,
            [new ReviewCommentRecord(draft.Id, command.ActorUserId, comment)],
            [Audit(command.ActorUserId, "instruction.return_to_draft", document.Id, command.RequestId)],
            ct);

        return updated;
    }

    public async Task<DocumentAggregate> RequestPublishAsync(RequestPublishCommand command, CancellationToken ct)
    {
        RequireRole(command.ActorRoles, "Admin");
        var document = await RequireDocumentAsync(command.InstructionId, ct);
        var draft = document.CurrentDraftVersion;
        if (document.State != InstructionState.InReview
            || draft is null
            || draft.State != InstructionVersionState.InReview)
        {
            throw new DocumentLifecycleException(
                "INVALID_LIFECYCLE_TRANSITION",
                409,
                "Only in-review drafts can be submitted for publication.");
        }

        var pending = document with
        {
            CurrentDraftVersion = draft with { IndexingStatus = IndexingStatus.Pending },
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await _repository.SaveAsync(
            pending,
            [],
            [Audit(command.ActorUserId, "instruction.publish_requested", document.Id, command.RequestId)],
            ct);

        var result = await _indexingClient.CreateIndexingJobAsync(
            new InternalIndexingRequest(
                document.Id,
                draft.Id,
                draft.ContentHtml,
                "published",
                Retry: draft.IndexingStatus == IndexingStatus.Failed),
            ct);

        if (!string.Equals(result.Status, "Succeeded", StringComparison.Ordinal))
        {
            var failed = pending with
            {
                CurrentDraftVersion = draft with
                {
                    IndexingStatus = IndexingStatus.Failed,
                    IndexingJobId = result.JobId,
                },
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            await _repository.SaveAsync(
                failed,
                [],
                [Audit(command.ActorUserId, "instruction.indexing_failed", document.Id, command.RequestId)],
                ct);
            throw new DocumentLifecycleException(
                "INDEXING_FAILED",
                502,
                "Pre-publication indexing failed.",
                new Dictionary<string, object?>
                {
                    ["indexingJobId"] = result.JobId,
                    ["errorCode"] = result.ErrorCode,
                });
        }

        var now = DateTimeOffset.UtcNow;
        var publishedVersion = draft with
        {
            State = InstructionVersionState.Published,
            PublishedAt = now,
            PublishedByUserId = command.ActorUserId,
            IndexingStatus = IndexingStatus.Succeeded,
            IndexingJobId = result.JobId,
        };
        var published = pending with
        {
            State = InstructionState.Published,
            CurrentDraftVersion = null,
            CurrentPublishedVersion = publishedVersion,
            UpdatedAt = now,
        };

        await _repository.SaveAsync(
            published,
            [],
            [Audit(command.ActorUserId, "instruction.published", document.Id, command.RequestId)],
            ct);

        return published;
    }

    public async Task<DocumentAggregate> ArchiveAsync(ArchiveInstructionCommand command, CancellationToken ct)
    {
        var document = await RequireDocumentAsync(command.InstructionId, ct);
        if (document.CurrentPublishedVersion is not null && !HasRole(command.ActorRoles, "Admin"))
        {
            throw new DocumentLifecycleException(
                "AUTH_FORBIDDEN",
                403,
                "Only administrators can archive instructions with an active published version.");
        }

        if (!HasRole(command.ActorRoles, "Admin") && document.State is not (InstructionState.Draft or InstructionState.InReview))
        {
            throw new DocumentLifecycleException("AUTH_FORBIDDEN", 403, "Actor cannot archive this instruction.");
        }

        var updated = document with
        {
            State = InstructionState.Archived,
            CurrentDraftVersion = document.CurrentDraftVersion is null
                ? null
                : document.CurrentDraftVersion with { State = InstructionVersionState.Archived },
            CurrentPublishedVersion = document.CurrentPublishedVersion is null
                ? null
                : document.CurrentPublishedVersion with { State = InstructionVersionState.Archived },
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await _repository.SaveAsync(
            updated,
            [],
            [Audit(command.ActorUserId, "instruction.archived", document.Id, command.RequestId)],
            ct);

        return updated;
    }

    public async Task<DocumentAggregate> RestoreAsync(RestoreInstructionCommand command, CancellationToken ct)
    {
        var document = await RequireDocumentAsync(command.InstructionId, ct);
        if (document.State != InstructionState.Archived)
        {
            throw new DocumentLifecycleException(
                "INVALID_LIFECYCLE_TRANSITION",
                409,
                "Only archived instructions can be restored.");
        }

        var now = DateTimeOffset.UtcNow;
        var nextVersionNumber = Math.Max(
            document.CurrentDraftVersion?.VersionNumber ?? 0,
            document.CurrentPublishedVersion?.VersionNumber ?? 0) + 1;
        var source = document.CurrentDraftVersion ?? document.CurrentPublishedVersion;
        var draft = new DocumentVersionRecord(
            Guid.NewGuid(),
            document.Id,
            nextVersionNumber,
            InstructionVersionState.Draft,
            source?.Title ?? document.Title,
            source?.InstructionType ?? string.Empty,
            source?.Audience ?? string.Empty,
            source?.ContentHtml ?? string.Empty,
            now,
                null,
                null,
                null,
                null,
                IndexingStatus.None,
                null);
        var updated = document with
        {
            State = InstructionState.Draft,
            CurrentDraftVersion = draft,
            CurrentPublishedVersion = null,
            UpdatedAt = now,
        };

        await _repository.SaveAsync(
            updated,
            [],
            [Audit(command.ActorUserId, "instruction.restored", document.Id, command.RequestId)],
            ct);

        return updated;
    }

    private async Task<DocumentAggregate> RequireDocumentAsync(Guid instructionId, CancellationToken ct)
    {
        return await _repository.FindAsync(instructionId, ct)
            ?? throw new DocumentLifecycleException("NOT_FOUND", 404, "Instruction not found.");
    }

    private static DocumentVersionRecord RequireDraft(DocumentAggregate document)
    {
        if (document.State != InstructionState.Draft
            || document.CurrentDraftVersion is null
            || document.CurrentDraftVersion.State != InstructionVersionState.Draft)
        {
            throw new DocumentLifecycleException(
                "INVALID_LIFECYCLE_TRANSITION",
                409,
                "No draft version is available.");
        }

        return document.CurrentDraftVersion;
    }

    private static void RequireReadyForReview(DocumentAggregate document, DocumentVersionRecord draft)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(draft.Title))
        {
            missing.Add("title");
        }

        if (string.IsNullOrWhiteSpace(draft.InstructionType))
        {
            missing.Add("instructionType");
        }

        if (string.IsNullOrWhiteSpace(draft.Audience))
        {
            missing.Add("audience");
        }

        if (document.AllowedGroupIds.Count == 0)
        {
            missing.Add("allowedGroupIds");
        }

        if (string.IsNullOrWhiteSpace(PlainText(draft.ContentHtml)))
        {
            missing.Add("contentHtml");
        }

        if (missing.Count > 0)
        {
            throw new DocumentLifecycleException(
                "VALIDATION_FAILED",
                400,
                "Instruction draft is missing required review fields.",
                new Dictionary<string, object?> { ["fields"] = missing });
        }
    }

    private static string PlainText(string html)
    {
        return HtmlTagPattern.Replace(html, string.Empty).Trim();
    }

    private static string RequireComment(string comment)
    {
        var normalized = comment.Trim();
        if (normalized.Length == 0)
        {
            throw new DocumentLifecycleException(
                "VALIDATION_FAILED",
                400,
                "A review comment is required.",
                new Dictionary<string, object?> { ["field"] = "comment" });
        }

        return normalized;
    }

    private static void RequireRole(IReadOnlyList<string> roles, string requiredRole)
    {
        if (!HasRole(roles, requiredRole))
        {
            throw new DocumentLifecycleException("AUTH_FORBIDDEN", 403, "Actor is not authorized.");
        }
    }

    private static bool HasRole(IReadOnlyList<string> roles, string role)
    {
        return roles.Contains(role, StringComparer.Ordinal);
    }

    private static IReadOnlyList<Guid> NormalizeGroupIds(IReadOnlyList<Guid> groupIds)
    {
        return groupIds.Distinct().Order().ToArray();
    }

    private static DocumentAuditEvent Audit(
        Guid actorUserId,
        string eventType,
        Guid documentId,
        string requestId)
    {
        return new DocumentAuditEvent(
            actorUserId,
            eventType,
            documentId,
            requestId,
            new Dictionary<string, object?> { ["instructionId"] = documentId });
    }
}

internal sealed class UnavailableInternalIndexingClient : IInternalIndexingClient
{
    public Task<InternalIndexingResult> CreateIndexingJobAsync(
        InternalIndexingRequest request,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new InternalIndexingResult(
            Guid.Empty,
            "Failed",
            0,
            "INDEXING_CLIENT_NOT_CONFIGURED",
            "Internal indexing client is not configured."));
    }
}
