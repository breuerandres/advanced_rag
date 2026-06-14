using System.Text.RegularExpressions;
using AdvancedRag.App.Auth;

namespace AdvancedRag.App.Documents;

public interface IDocumentLifecycleService
{
    Task<IReadOnlyList<DocumentSummary>> ListAsync(CancellationToken ct);

    Task<DocumentAggregate> CreateDraftAsync(CreateDocumentCommand command, CancellationToken ct);

    Task<DocumentAggregate?> GetAsync(Guid documentId, CancellationToken ct);

    Task<DocumentAggregate> UpdateDraftAsync(UpdateDraftCommand command, CancellationToken ct);

    Task<DocumentAggregate> SendToReviewAsync(SendToReviewCommand command, CancellationToken ct);

    Task<DocumentAggregate> ReturnToDraftAsync(ReturnToDraftCommand command, CancellationToken ct);

    Task<DocumentAggregate> RequestPublishAsync(RequestPublishCommand command, CancellationToken ct);

    Task<DocumentAggregate> ArchiveAsync(ArchiveDocumentCommand command, CancellationToken ct);

    Task<DocumentAggregate> RestoreAsync(RestoreDocumentCommand command, CancellationToken ct);
}

public sealed class DocumentLifecycleService : IDocumentLifecycleService
{
    private static readonly Regex HtmlTagPattern = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex ImageTagPattern = new(
        "<img\\b[^>]*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ImageSrcPattern = new(
        "\\bsrc\\s*=\\s*(?:\"(?<src>[^\"]*)\"|'(?<src>[^']*)'|(?<src>[^\\s>]+))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex StableDocumentImageSourcePattern = new(
        "^/api/document-images/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}/content$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IDocumentRepository _repository;
    private readonly IInternalIndexingClient _indexingClient;
    private readonly IInternalCacheInvalidationClient _cacheInvalidation;
    private readonly IDocumentHtmlSanitizer _htmlSanitizer;
    private readonly IEffectiveAccessScopeRepository? _accessScopes;
    private readonly IDocumentAccessPolicy? _accessPolicy;
    private readonly IDocumentTypeRepository? _documentTypes;

    public DocumentLifecycleService(
        IDocumentRepository repository,
        IDocumentHtmlSanitizer? htmlSanitizer = null,
        IInternalIndexingClient? indexingClient = null,
        IEffectiveAccessScopeRepository? accessScopes = null,
        IDocumentAccessPolicy? accessPolicy = null,
        IInternalCacheInvalidationClient? cacheInvalidationClient = null,
        IDocumentTypeRepository? documentTypes = null)
    {
        _repository = repository;
        _indexingClient = indexingClient ?? new UnavailableInternalIndexingClient();
        _cacheInvalidation = cacheInvalidationClient ?? new NoOpInternalCacheInvalidationClient();
        _htmlSanitizer = htmlSanitizer ?? new PassthroughDocumentHtmlSanitizer();
        _accessScopes = accessScopes;
        _accessPolicy = accessPolicy;
        _documentTypes = documentTypes;
    }

    public Task<IReadOnlyList<DocumentSummary>> ListAsync(CancellationToken ct)
    {
        return _repository.ListAsync(ct);
    }

    public Task<DocumentAggregate?> GetAsync(Guid documentId, CancellationToken ct)
    {
        return _repository.FindAsync(documentId, ct);
    }

    public async Task<DocumentAggregate> CreateDraftAsync(CreateDocumentCommand command, CancellationToken ct)
    {
        (Guid? documentTypeId, string documentTypeName) = await ResolveTypeAsync(command.DocumentTypeId, ct);
        DocumentAggregate document = DocumentAggregate.NewDraft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            command.Title.Trim(),
            documentTypeId,
            documentTypeName,
            command.Audience.Trim(),
            SanitizeDocumentHtml(command.ContentHtml),
            NormalizeRules(command.AccessRules),
            command.ActorUserId);

        await RequireCanManageDraftAsync(command.ActorUserId, document.AccessRules, ct);

        await _repository.SaveAsync(
            document,
            [],
            [Audit(command.ActorUserId, "document.created", document.Id, command.RequestId)],
            ct);

        return document;
    }

    public async Task<DocumentAggregate> UpdateDraftAsync(UpdateDraftCommand command, CancellationToken ct)
    {
        DocumentAggregate document = await RequireDocumentAsync(command.DocumentId, ct);
        if (document.State == DocumentState.Archived)
        {
            throw new DocumentLifecycleException(
                "INVALID_LIFECYCLE_TRANSITION",
                409,
                "Archived documents must be restored before editing.");
        }

        string normalizedTitle = command.Title.Trim();
        (Guid? documentTypeId, string documentTypeName) = await ResolveTypeAsync(command.DocumentTypeId, ct);
        string normalizedAudience = command.Audience.Trim();
        string normalizedContent = SanitizeDocumentHtml(command.ContentHtml);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DocumentVersionRecord? existingDraft = document.CurrentDraftVersion;

        DocumentVersionRecord draft;
        if (existingDraft is null)
        {
            int nextVersionNumber = (document.CurrentPublishedVersion?.VersionNumber ?? 0) + 1;
            draft = new DocumentVersionRecord(
                Guid.NewGuid(),
                document.Id,
                nextVersionNumber,
                DocumentVersionState.Draft,
                normalizedTitle,
                documentTypeId,
                documentTypeName,
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
            if (existingDraft.State != DocumentVersionState.Draft)
            {
                throw new DocumentLifecycleException(
                    "INVALID_LIFECYCLE_TRANSITION",
                    409,
                    "Only draft versions can be edited.");
            }

            draft = existingDraft with
            {
                Title = normalizedTitle,
                DocumentTypeId = documentTypeId,
                DocumentType = documentTypeName,
                Audience = normalizedAudience,
                ContentHtml = normalizedContent,
                IndexingStatus = IndexingStatus.None,
                IndexingJobId = null,
            };
        }

        DocumentAggregate updated = document with
        {
            Title = normalizedTitle,
            State = DocumentState.Draft,
            CurrentDraftVersion = draft,
            AccessRules = NormalizeRules(command.AccessRules),
            UpdatedAt = now,
        };

        await RequireCanManageDraftAsync(command.ActorUserId, updated.AccessRules, ct);

        // Editing a previously published document (content and/or access rules) ends its
        // published availability and may change who can see it; any cached answers sourced
        // from the old published version are now stale. Fresh drafts have nothing cached.
        bool wasPublished = document.CurrentPublishedVersion is not null;

        await _repository.SaveAsync(
            updated,
            [],
            [Audit(command.ActorUserId, "document.draft_updated", document.Id, command.RequestId)],
            ct);

        if (wasPublished)
        {
            await _cacheInvalidation.InvalidateDocumentsAsync([document.Id], ct);
        }

        return updated;
    }

    public async Task<DocumentAggregate> SendToReviewAsync(SendToReviewCommand command, CancellationToken ct)
    {
        DocumentAggregate document = await RequireDocumentAsync(command.DocumentId, ct);
        DocumentVersionRecord draft = RequireDraft(document);
        await RequireCanManageDraftAsync(command.ActorUserId, document.AccessRules, ct);
        RequireReadyForReview(document, draft);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        DocumentVersionRecord updatedDraft = draft with
        {
            State = DocumentVersionState.InReview,
            SubmittedForReviewAt = now,
            SubmittedForReviewByUserId = command.ActorUserId,
        };
        DocumentAggregate updated = document with
        {
            State = DocumentState.InReview,
            CurrentDraftVersion = updatedDraft,
            UpdatedAt = now,
        };
        IReadOnlyList<ReviewCommentRecord> comments = string.IsNullOrWhiteSpace(command.Comment)
            ? Array.Empty<ReviewCommentRecord>()
            : [new ReviewCommentRecord(draft.Id, command.ActorUserId, command.Comment.Trim())];

        await _repository.SaveAsync(
            updated,
            comments,
            [Audit(command.ActorUserId, "document.send_to_review", document.Id, command.RequestId)],
            ct);

        return updated;
    }

    public async Task<DocumentAggregate> ReturnToDraftAsync(ReturnToDraftCommand command, CancellationToken ct)
    {
        string comment = RequireComment(command.Comment);
        DocumentAggregate document = await RequireDocumentAsync(command.DocumentId, ct);
        await RequireCanManageDraftAsync(command.ActorUserId, document.AccessRules, ct);
        DocumentVersionRecord? draft = document.CurrentDraftVersion;
        if (document.State != DocumentState.InReview
            || draft is null
            || draft.State != DocumentVersionState.InReview)
        {
            throw new DocumentLifecycleException(
                "INVALID_LIFECYCLE_TRANSITION",
                409,
                "Only in-review drafts can be returned to draft.");
        }

        DocumentVersionRecord updatedDraft = draft with
        {
            State = DocumentVersionState.Draft,
            SubmittedForReviewAt = null,
            SubmittedForReviewByUserId = null,
            IndexingStatus = IndexingStatus.None,
        };
        DocumentAggregate updated = document with
        {
            State = DocumentState.Draft,
            CurrentDraftVersion = updatedDraft,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await _repository.SaveAsync(
            updated,
            [new ReviewCommentRecord(draft.Id, command.ActorUserId, comment)],
            [Audit(command.ActorUserId, "document.return_to_draft", document.Id, command.RequestId)],
            ct);

        return updated;
    }

    public async Task<DocumentAggregate> RequestPublishAsync(RequestPublishCommand command, CancellationToken ct)
    {
        DocumentAggregate document = await RequireDocumentAsync(command.DocumentId, ct);
        await RequireCanPublishAsync(command.ActorUserId, command.ActorRoles, document.AccessRules, ct);
        DocumentVersionRecord? draft = document.CurrentDraftVersion;
        if (document.State != DocumentState.InReview
            || draft is null
            || draft.State != DocumentVersionState.InReview)
        {
            throw new DocumentLifecycleException(
                "INVALID_LIFECYCLE_TRANSITION",
                409,
                "Only in-review drafts can be submitted for publication.");
        }

        DocumentAggregate pending = document with
        {
            CurrentDraftVersion = draft with { IndexingStatus = IndexingStatus.Pending },
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await _repository.SaveAsync(
            pending,
            [],
            [Audit(command.ActorUserId, "document.publish_requested", document.Id, command.RequestId)],
            ct);

        InternalIndexingResult result = await _indexingClient.CreateIndexingJobAsync(
            new InternalIndexingRequest(
                document.Id,
                draft.Id,
                draft.ContentHtml,
                "published",
                Retry: draft.IndexingStatus == IndexingStatus.Failed),
            ct);

        if (!string.Equals(result.Status, "Succeeded", StringComparison.Ordinal))
        {
            DocumentAggregate failed = pending with
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
                [Audit(command.ActorUserId, "document.indexing_failed", document.Id, command.RequestId)],
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

        DateTimeOffset now = DateTimeOffset.UtcNow;
        DocumentVersionRecord publishedVersion = draft with
        {
            State = DocumentVersionState.Published,
            PublishedAt = now,
            PublishedByUserId = command.ActorUserId,
            IndexingStatus = IndexingStatus.Succeeded,
            IndexingJobId = result.JobId,
        };
        DocumentAggregate published = pending with
        {
            State = DocumentState.Published,
            CurrentDraftVersion = null,
            CurrentPublishedVersion = publishedVersion,
            UpdatedAt = now,
        };

        // Capture before relying on the replacement aggregate: a republish replaces a
        // previous published version, so any cached answers sourced from it are stale.
        bool isRepublish = document.CurrentPublishedVersion is not null;

        await _repository.SaveAsync(
            published,
            [],
            [Audit(command.ActorUserId, "document.published", document.Id, command.RequestId)],
            ct);

        if (isRepublish)
        {
            await _cacheInvalidation.InvalidateDocumentsAsync([document.Id], ct);
        }

        return published;
    }

    public async Task<DocumentAggregate> ArchiveAsync(ArchiveDocumentCommand command, CancellationToken ct)
    {
        DocumentAggregate document = await RequireDocumentAsync(command.DocumentId, ct);
        EffectiveAccessScope scope = await ResolveScopeAsync(command.ActorUserId, command.ActorRoles, ct);
        bool canPublish = _accessPolicy is null
            ? HasRole(command.ActorRoles, "Admin")
            : await _accessPolicy.CanPublishAsync(scope, document.AccessRules, ct);
        bool canManageDraft = _accessPolicy is null
            ? HasRole(command.ActorRoles, "Admin") || document.State is DocumentState.Draft or DocumentState.InReview
            : await _accessPolicy.CanManageDraftAsync(scope, document.AccessRules, ct);
        if (document.CurrentPublishedVersion is not null && !canPublish)
        {
            throw new DocumentLifecycleException(
                "AUTH_FORBIDDEN",
                403,
                "Actor cannot archive this published document.");
        }

        if (!canPublish && !canManageDraft)
        {
            throw new DocumentLifecycleException("AUTH_FORBIDDEN", 403, "Actor cannot archive this document.");
        }

        DocumentAggregate updated = document with
        {
            State = DocumentState.Archived,
            CurrentDraftVersion = document.CurrentDraftVersion is null
                ? null
                : document.CurrentDraftVersion with { State = DocumentVersionState.Archived },
            CurrentPublishedVersion = document.CurrentPublishedVersion is null
                ? null
                : document.CurrentPublishedVersion with { State = DocumentVersionState.Archived },
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await _repository.SaveAsync(
            updated,
            [],
            [Audit(command.ActorUserId, "document.archived", document.Id, command.RequestId)],
            ct);

        await _cacheInvalidation.InvalidateDocumentsAsync([document.Id], ct);

        return updated;
    }

    public async Task<DocumentAggregate> RestoreAsync(RestoreDocumentCommand command, CancellationToken ct)
    {
        DocumentAggregate document = await RequireDocumentAsync(command.DocumentId, ct);
        await RequireCanManageDraftAsync(command.ActorUserId, document.AccessRules, ct);
        if (document.State != DocumentState.Archived)
        {
            throw new DocumentLifecycleException(
                "INVALID_LIFECYCLE_TRANSITION",
                409,
                "Only archived documents can be restored.");
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        int nextVersionNumber = Math.Max(
            document.CurrentDraftVersion?.VersionNumber ?? 0,
            document.CurrentPublishedVersion?.VersionNumber ?? 0) + 1;
        DocumentVersionRecord? source = document.CurrentDraftVersion ?? document.CurrentPublishedVersion;
        DocumentVersionRecord draft = new(
            Guid.NewGuid(),
            document.Id,
            nextVersionNumber,
            DocumentVersionState.Draft,
            source?.Title ?? document.Title,
            source?.DocumentTypeId,
            source?.DocumentType ?? string.Empty,
            source?.Audience ?? string.Empty,
            source?.ContentHtml ?? string.Empty,
            now,
                null,
                null,
                null,
                null,
                IndexingStatus.None,
                null);
        DocumentAggregate updated = document with
        {
            State = DocumentState.Draft,
            CurrentDraftVersion = draft,
            CurrentPublishedVersion = null,
            UpdatedAt = now,
        };

        await _repository.SaveAsync(
            updated,
            [],
            [Audit(command.ActorUserId, "document.restored", document.Id, command.RequestId)],
            ct);

        await _cacheInvalidation.InvalidateDocumentsAsync([document.Id], ct);

        return updated;
    }

    private async Task<DocumentAggregate> RequireDocumentAsync(Guid documentId, CancellationToken ct)
    {
        return await _repository.FindAsync(documentId, ct)
            ?? throw new DocumentLifecycleException("NOT_FOUND", 404, "Document not found.");
    }

    private async Task<(Guid? Id, string Name)> ResolveTypeAsync(Guid? documentTypeId, CancellationToken ct)
    {
        if (documentTypeId is null)
        {
            return (null, string.Empty);
        }

        if (_documentTypes is null)
        {
            return (documentTypeId, string.Empty);
        }

        DocumentTypeRecord? record = await _documentTypes.FindAsync(documentTypeId.Value, ct);
        if (record is null)
        {
            throw new DocumentLifecycleException(
                "VALIDATION_FAILED",
                400,
                "Document type is invalid.",
                new Dictionary<string, object?> { ["field"] = "documentTypeId" });
        }

        return (record.Id, record.Name);
    }

    private static DocumentVersionRecord RequireDraft(DocumentAggregate document)
    {
        if (document.State != DocumentState.Draft
            || document.CurrentDraftVersion is null
            || document.CurrentDraftVersion.State != DocumentVersionState.Draft)
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
        List<string> missing = new();
        if (string.IsNullOrWhiteSpace(draft.Title))
        {
            missing.Add("title");
        }

        if (draft.DocumentTypeId is null)
        {
            missing.Add("documentType");
        }

        if (string.IsNullOrWhiteSpace(draft.Audience))
        {
            missing.Add("audience");
        }

        if (document.AccessRules.Count == 0 || document.AccessRules.Any(IsInvalidRule))
        {
            missing.Add("accessRules");
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
                "Document draft is missing required review fields.",
                new Dictionary<string, object?> { ["fields"] = missing });
        }
    }

    private static string PlainText(string html)
    {
        return HtmlTagPattern.Replace(html, string.Empty).Trim();
    }

    private string SanitizeDocumentHtml(string html)
    {
        string trimmed = html.Trim();
        ValidateImageSources(trimmed);
        return _htmlSanitizer.Sanitize(trimmed);
    }

    private static void ValidateImageSources(string html)
    {
        foreach (Match imageMatch in ImageTagPattern.Matches(html))
        {
            Match sourceMatch = ImageSrcPattern.Match(imageMatch.Value);
            if (!sourceMatch.Success)
            {
                continue;
            }

            string source = sourceMatch.Groups["src"].Value.Trim();
            if (StableDocumentImageSourcePattern.IsMatch(source))
            {
                continue;
            }

            throw new DocumentLifecycleException(
                "DOCUMENT_IMAGE_SOURCE_INVALID",
                400,
                "Document image sources must use stable app-controlled URLs.",
                new Dictionary<string, object?> { ["field"] = "contentHtml" });
        }
    }

    private static string RequireComment(string comment)
    {
        string normalized = comment.Trim();
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

    private static bool HasRole(IReadOnlyList<string> roles, string role)
    {
        return roles.Contains(role, StringComparer.Ordinal);
    }

    private async Task RequireCanManageDraftAsync(
        Guid actorUserId,
        IReadOnlyList<DocumentAccessRuleRecord> rules,
        CancellationToken ct)
    {
        if (_accessPolicy is null)
        {
            return;
        }

        EffectiveAccessScope scope = await RequireScopeAsync(actorUserId, ct);
        if (!await _accessPolicy.CanManageDraftAsync(scope, rules, ct))
        {
            throw new DocumentLifecycleException("AUTH_FORBIDDEN", 403, "Actor cannot manage this document.");
        }
    }

    private async Task RequireCanPublishAsync(
        Guid actorUserId,
        IReadOnlyList<string> actorRoles,
        IReadOnlyList<DocumentAccessRuleRecord> rules,
        CancellationToken ct)
    {
        if (_accessPolicy is null)
        {
            if (!HasRole(actorRoles, "Admin"))
            {
                throw new DocumentLifecycleException("AUTH_FORBIDDEN", 403, "Actor is not authorized.");
            }

            return;
        }

        EffectiveAccessScope scope = await ResolveScopeAsync(actorUserId, actorRoles, ct);
        if (!await _accessPolicy.CanPublishAsync(scope, rules, ct))
        {
            throw new DocumentLifecycleException("AUTH_FORBIDDEN", 403, "Actor cannot publish this document.");
        }
    }

    private async Task<EffectiveAccessScope> ResolveScopeAsync(
        Guid actorUserId,
        IReadOnlyList<string> actorRoles,
        CancellationToken ct)
    {
        if (_accessScopes is not null)
        {
            return await RequireScopeAsync(actorUserId, ct);
        }

        string primaryRole = PrimaryRole(actorRoles);
        return new EffectiveAccessScope(
            actorUserId,
            primaryRole,
            primaryRole.Equals("Admin", StringComparison.Ordinal),
            DocumentAccessPolicy.RootOrganizationalUnitId,
            [],
            1,
            "published");
    }

    private async Task<EffectiveAccessScope> RequireScopeAsync(Guid actorUserId, CancellationToken ct)
    {
        return _accessScopes is null
            ? throw new DocumentLifecycleException("AUTH_FORBIDDEN", 403, "Actor is not authorized.")
            : await _accessScopes.FindForActiveUserAsync(actorUserId, ct)
                ?? throw new DocumentLifecycleException("AUTH_FORBIDDEN", 403, "Actor is not authorized.");
    }

    private static string PrimaryRole(IReadOnlyList<string> roles)
    {
        if (HasRole(roles, "Admin"))
        {
            return "Admin";
        }

        if (HasRole(roles, "DocumentPublisher"))
        {
            return "DocumentPublisher";
        }

        if (HasRole(roles, "DocumentEditor"))
        {
            return "DocumentEditor";
        }

        return "Viewer";
    }

    private static IReadOnlyList<DocumentAccessRuleRecord> NormalizeRules(IReadOnlyList<DocumentAccessRuleDraft> rules)
    {
        if (rules.Count == 0 || rules.Any(rule => rule.OrganizationalUnitId is null && rule.GroupIds.Count == 0))
        {
            throw new DocumentLifecycleException(
                "VALIDATION_FAILED",
                400,
                "At least one document access rule is required.",
                new Dictionary<string, object?> { ["field"] = "accessRules" });
        }

        return rules
            .Select(rule => new DocumentAccessRuleRecord(
                Guid.NewGuid(),
                rule.OrganizationalUnitId,
                rule.GroupIds.Distinct().Order().ToArray()))
            .ToArray();
    }

    private static bool IsInvalidRule(DocumentAccessRuleRecord rule)
    {
        return rule.OrganizationalUnitId is null && rule.GroupIds.Count == 0;
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
            new Dictionary<string, object?> { ["documentId"] = documentId });
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

internal sealed class NoOpInternalCacheInvalidationClient : IInternalCacheInvalidationClient
{
    public Task<int> InvalidateDocumentsAsync(IReadOnlyList<Guid> documentIds, CancellationToken ct)
        => Task.FromResult(0);
}
