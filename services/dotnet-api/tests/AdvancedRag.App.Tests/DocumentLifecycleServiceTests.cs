using AdvancedRag.App.Documents;
using AdvancedRag.App.Auth;
using FluentAssertions;

namespace AdvancedRag.App.Tests;

public sealed class DocumentLifecycleServiceTests
{
    private static readonly Guid ActorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DocumentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid VersionId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid OperationsGroupId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid EmpresaUnitId = DocumentAccessPolicy.RootOrganizationalUnitId;
    private static readonly Guid ComunicacionUnitId = Guid.Parse("01000000-0000-0000-0000-000000000002");
    private static readonly Guid MarketingUnitId = Guid.Parse("01000000-0000-0000-0000-000000000003");
    private static readonly Guid SistemasUnitId = Guid.Parse("01000000-0000-0000-0000-000000000004");
    private static readonly Guid ComiteCrisisGroupId = Guid.Parse("02000000-0000-0000-0000-000000000002");
    private static readonly Guid GerentesGroupId = Guid.Parse("02000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task SendToReviewAsync_RejectsDraftMissingRequiredReviewFields()
    {
        var repository = new InMemoryDocumentRepository();
        repository.Documents[DocumentId] = DocumentAggregate.NewDraft(
            DocumentId,
            VersionId,
            "",
            "",
            "",
            "<p>   </p>",
            [],
            ActorId);
        var service = new DocumentLifecycleService(repository);

        var act = () => service.SendToReviewAsync(
            new SendToReviewCommand(DocumentId, "Ready", ActorId, "request-1"),
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<DocumentLifecycleException>()
            .Where(error => error.Code == "VALIDATION_FAILED");
    }

    [Fact]
    public async Task SendToReviewAsync_DocumentManagerCanMoveValidDraftToReview()
    {
        var repository = new InMemoryDocumentRepository();
        repository.Documents[DocumentId] = ValidDraft();
        var service = new DocumentLifecycleService(repository);

        var document = await service.SendToReviewAsync(
            new SendToReviewCommand(DocumentId, "Ready for review", ActorId, "request-2"),
            CancellationToken.None);

        document.State.Should().Be(DocumentState.InReview);
        document.CurrentDraftVersion!.State.Should().Be(DocumentVersionState.InReview);
        repository.ReviewComments.Should().ContainSingle(comment =>
            comment.DocumentVersionId == VersionId && comment.Comment == "Ready for review");
        repository.AuditEvents.Should().ContainSingle(audit =>
            audit.EventType == "document.send_to_review" && audit.RequestId == "request-2");
    }

    [Fact]
    public async Task RequestPublishAsync_RejectsDocumentManager()
    {
        var repository = new InMemoryDocumentRepository();
        repository.Documents[DocumentId] = ValidInReview();
        var service = new DocumentLifecycleService(repository);

        var act = () => service.RequestPublishAsync(
            new RequestPublishCommand(DocumentId, ActorId, ["DocumentManager"], "request-3"),
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<DocumentLifecycleException>()
            .Where(error => error.Code == "AUTH_FORBIDDEN");
    }

    [Fact]
    public async Task RequestPublishAsync_IndexingSuccessPublishesVersion()
    {
        var repository = new InMemoryDocumentRepository();
        repository.Documents[DocumentId] = ValidInReview();
        var indexing = new RecordingIndexingClient();
        var service = new DocumentLifecycleService(repository, indexingClient: indexing);

        var document = await service.RequestPublishAsync(
            new RequestPublishCommand(DocumentId, ActorId, ["Admin"], "request-4"),
            CancellationToken.None);

        document.State.Should().Be(DocumentState.Published);
        document.CurrentDraftVersion.Should().BeNull();
        document.CurrentPublishedVersion!.IndexingStatus.Should().Be(IndexingStatus.Succeeded);
        document.CurrentPublishedVersion.IndexingJobId.Should().Be(indexing.JobId);
        indexing.Requests.Should().ContainSingle(request =>
            request.DocumentId == DocumentId
            && request.DocumentVersionId == VersionId
            && request.CorpusMode == "published"
            && request.ContentHtml == "<p>Wear protective equipment.</p>");
        repository.AuditEvents.Should().ContainSingle(audit =>
            audit.EventType == "document.publish_requested");
        repository.AuditEvents.Should().ContainSingle(audit =>
            audit.EventType == "document.published");
    }

    [Fact]
    public async Task RequestPublishAsync_WhenIndexingFailsLeavesDocumentInReviewWithSafeFailure()
    {
        var repository = new InMemoryDocumentRepository();
        repository.Documents[DocumentId] = ValidInReview();
        var service = new DocumentLifecycleService(
            repository,
            indexingClient: new FailingIndexingClient());

        var act = () => service.RequestPublishAsync(
            new RequestPublishCommand(DocumentId, ActorId, ["Admin"], "request-index-fail"),
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<DocumentLifecycleException>()
            .Where(error => error.Code == "INDEXING_FAILED");
        repository.Documents[DocumentId].State.Should().Be(DocumentState.InReview);
        repository.Documents[DocumentId].CurrentDraftVersion!.IndexingStatus.Should().Be(IndexingStatus.Failed);
    }

    [Fact]
    public async Task ReturnToDraftAsync_RequiresComment()
    {
        var repository = new InMemoryDocumentRepository();
        repository.Documents[DocumentId] = ValidInReview();
        var service = new DocumentLifecycleService(repository);

        var act = () => service.ReturnToDraftAsync(
            new ReturnToDraftCommand(DocumentId, " ", ActorId, "request-5"),
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<DocumentLifecycleException>()
            .Where(error => error.Code == "VALIDATION_FAILED");
    }

    [Fact]
    public async Task UpdateDraftAsync_WhenPublishedDocumentIsEditedCreatesNewDraftVersion()
    {
        var repository = new InMemoryDocumentRepository();
        repository.Documents[DocumentId] = PublishedDocument();
        var service = new DocumentLifecycleService(repository);

        var document = await service.UpdateDraftAsync(
            new UpdateDraftCommand(
                DocumentId,
                "Updated safety policy",
                "Policy",
                "All staff",
                "<p>Updated content</p>",
                GroupRules(OperationsGroupId),
                ActorId,
                "request-6"),
            CancellationToken.None);

        document.State.Should().Be(DocumentState.Draft);
        document.CurrentPublishedVersion.Should().NotBeNull();
        document.CurrentDraftVersion.Should().NotBeNull();
        document.CurrentDraftVersion!.VersionNumber.Should().Be(2);
        document.CurrentPublishedVersion!.VersionNumber.Should().Be(1);
    }

    [Fact]
    public async Task UpdateDraftAsync_SanitizesHtmlBeforeSavingDraft()
    {
        var repository = new InMemoryDocumentRepository();
        repository.Documents[DocumentId] = ValidDraft();
        var service = new DocumentLifecycleService(repository, new StubHtmlSanitizer());

        var document = await service.UpdateDraftAsync(
            new UpdateDraftCommand(
                DocumentId,
                "Safety policy",
                "Policy",
                "All staff",
                "<script>alert(1)</script><p>Safe content</p>",
                GroupRules(OperationsGroupId),
                ActorId,
                "request-sanitize"),
            CancellationToken.None);

        document.CurrentDraftVersion!.ContentHtml.Should().Be("<p>Safe content</p>");
    }

    [Theory]
    [InlineData("<p>Unsafe</p><img src=\"https://cdn.example.com/image.png\" alt=\"external\">")]
    [InlineData("<p>Unsafe</p><img src=\"data:image/png;base64,AAAA\" alt=\"inline\">")]
    public async Task UpdateDraftAsync_RejectsNonAppImageSources(string contentHtml)
    {
        var repository = new InMemoryDocumentRepository();
        repository.Documents[DocumentId] = ValidDraft();
        var service = new DocumentLifecycleService(repository, new StubHtmlSanitizer());

        var act = () => service.UpdateDraftAsync(
            new UpdateDraftCommand(
                DocumentId,
                "Safety policy",
                "Policy",
                "All staff",
                contentHtml,
                GroupRules(OperationsGroupId),
                ActorId,
                "request-invalid-image"),
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<DocumentLifecycleException>()
            .Where(error => error.Code == "DOCUMENT_IMAGE_SOURCE_INVALID");
    }

    [Fact]
    public async Task UpdateDraftAsync_AllowsStableDocumentImageSources()
    {
        var repository = new InMemoryDocumentRepository();
        repository.Documents[DocumentId] = ValidDraft();
        var service = new DocumentLifecycleService(repository, new StubHtmlSanitizer());
        var imageId = Guid.Parse("77777777-7777-7777-7777-777777777777");

        var document = await service.UpdateDraftAsync(
            new UpdateDraftCommand(
                DocumentId,
                "Safety policy",
                "Policy",
                "All staff",
                $"<p>Safe content</p><img src=\"/api/document-images/{imageId}/content\" alt=\"diagram\">",
                GroupRules(OperationsGroupId),
                ActorId,
                "request-valid-image"),
            CancellationToken.None);

        document.CurrentDraftVersion!.ContentHtml.Should()
            .Contain($"/api/document-images/{imageId}/content");
    }

    [Fact]
    public async Task RequestPublishAsync_PublisherCanUseOwnedGroupButCannotUseGlobalGroupWithoutGrant()
    {
        var repository = new InMemoryDocumentRepository();
        repository.Documents[DocumentId] = ValidInReview([
            Rule(ComunicacionUnitId, ComiteCrisisGroupId),
        ]);
        var scopes = new InMemoryEffectiveAccessScopeRepository();
        scopes.Scopes[ActorId] = Scope("DocumentPublisher", ComunicacionUnitId, [ComiteCrisisGroupId, GerentesGroupId]);
        var accessData = HierarchyAccessDataSource();
        accessData.GroupPolicies[ComiteCrisisGroupId] = new DocumentAccessGroupPolicy(
            ComiteCrisisGroupId,
            ComunicacionUnitId,
            "OwnerScope",
            false);
        accessData.GroupPolicies[GerentesGroupId] = new DocumentAccessGroupPolicy(
            GerentesGroupId,
            null,
            "ExplicitGrantOnly",
            false);
        var service = new DocumentLifecycleService(
            repository,
            indexingClient: new RecordingIndexingClient(),
            accessScopes: scopes,
            accessPolicy: new DocumentAccessPolicy(accessData));

        DocumentAggregate document = await service.RequestPublishAsync(
            new RequestPublishCommand(DocumentId, ActorId, ["DocumentPublisher"], "request-owned-group"),
            CancellationToken.None);

        document.State.Should().Be(DocumentState.Published);

        repository.Documents[DocumentId] = ValidInReview([
            Rule(ComunicacionUnitId, GerentesGroupId),
        ]);
        Func<Task> act = () => service.RequestPublishAsync(
            new RequestPublishCommand(DocumentId, ActorId, ["DocumentPublisher"], "request-global-group"),
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<DocumentLifecycleException>()
            .Where(error => error.Code == "AUTH_FORBIDDEN");
    }

    [Fact]
    public async Task RequestPublishAsync_PublisherCannotPublishOutsideOwnBranch()
    {
        var repository = new InMemoryDocumentRepository();
        repository.Documents[DocumentId] = ValidInReview([Rule(SistemasUnitId)]);
        var scopes = new InMemoryEffectiveAccessScopeRepository();
        scopes.Scopes[ActorId] = Scope("DocumentPublisher", ComunicacionUnitId, []);
        var service = new DocumentLifecycleService(
            repository,
            indexingClient: new RecordingIndexingClient(),
            accessScopes: scopes,
            accessPolicy: new DocumentAccessPolicy(HierarchyAccessDataSource()));

        Func<Task> act = () => service.RequestPublishAsync(
            new RequestPublishCommand(DocumentId, ActorId, ["DocumentPublisher"], "request-sibling"),
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<DocumentLifecycleException>()
            .Where(error => error.Code == "AUTH_FORBIDDEN");
    }

    [Fact]
    public async Task ArchiveAsync_DocumentManagerCannotArchiveActivePublishedDocument()
    {
        var repository = new InMemoryDocumentRepository();
        repository.Documents[DocumentId] = PublishedDocument();
        var service = new DocumentLifecycleService(repository);

        var act = () => service.ArchiveAsync(
            new ArchiveDocumentCommand(DocumentId, ActorId, ["DocumentManager"], "request-7"),
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<DocumentLifecycleException>()
            .Where(error => error.Code == "AUTH_FORBIDDEN");
    }

    [Fact]
    public async Task RestoreAsync_ArchivedDocumentReturnsToDraftWithoutReactivatingPublishedVersion()
    {
        var repository = new InMemoryDocumentRepository();
        var archived = PublishedDocument() with { State = DocumentState.Archived };
        repository.Documents[DocumentId] = archived;
        var service = new DocumentLifecycleService(repository);

        var document = await service.RestoreAsync(
            new RestoreDocumentCommand(DocumentId, ActorId, "request-8"),
            CancellationToken.None);

        document.State.Should().Be(DocumentState.Draft);
        document.CurrentPublishedVersion.Should().BeNull();
        document.CurrentDraftVersion.Should().NotBeNull();
        document.CurrentDraftVersion!.VersionNumber.Should().Be(2);
    }

    [Fact]
    public async Task ArchiveAsync_InvalidatesSemanticCacheForDocument()
    {
        var repository = new InMemoryDocumentRepository();
        repository.Documents[DocumentId] = PublishedDocument();
        var cache = new RecordingCacheInvalidationClient();
        var service = new DocumentLifecycleService(repository, cacheInvalidationClient: cache);

        await service.ArchiveAsync(
            new ArchiveDocumentCommand(DocumentId, ActorId, ["Admin"], "request-archive-cache"),
            CancellationToken.None);

        cache.Calls.Single().Single().Should().Be(DocumentId);
    }

    [Fact]
    public async Task RestoreAsync_InvalidatesSemanticCacheForDocument()
    {
        var repository = new InMemoryDocumentRepository();
        repository.Documents[DocumentId] = PublishedDocument() with { State = DocumentState.Archived };
        var cache = new RecordingCacheInvalidationClient();
        var service = new DocumentLifecycleService(repository, cacheInvalidationClient: cache);

        await service.RestoreAsync(
            new RestoreDocumentCommand(DocumentId, ActorId, "request-restore-cache"),
            CancellationToken.None);

        cache.Calls.Single().Single().Should().Be(DocumentId);
    }

    [Fact]
    public async Task RequestPublishAsync_RepublishInvalidatesSemanticCacheForDocument()
    {
        var repository = new InMemoryDocumentRepository();
        repository.Documents[DocumentId] = ValidInReview() with
        {
            CurrentPublishedVersion = PublishedDocument().CurrentPublishedVersion,
        };
        var cache = new RecordingCacheInvalidationClient();
        var service = new DocumentLifecycleService(
            repository,
            indexingClient: new RecordingIndexingClient(),
            cacheInvalidationClient: cache);

        await service.RequestPublishAsync(
            new RequestPublishCommand(DocumentId, ActorId, ["Admin"], "request-republish-cache"),
            CancellationToken.None);

        cache.Calls.Single().Single().Should().Be(DocumentId);
    }

    [Fact]
    public async Task RequestPublishAsync_FirstPublishDoesNotInvalidateSemanticCache()
    {
        var repository = new InMemoryDocumentRepository();
        repository.Documents[DocumentId] = ValidInReview();
        var cache = new RecordingCacheInvalidationClient();
        var service = new DocumentLifecycleService(
            repository,
            indexingClient: new RecordingIndexingClient(),
            cacheInvalidationClient: cache);

        await service.RequestPublishAsync(
            new RequestPublishCommand(DocumentId, ActorId, ["Admin"], "request-first-publish-cache"),
            CancellationToken.None);

        cache.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateDraftAsync_OnPublishedDocumentInvalidatesSemanticCache()
    {
        var repository = new InMemoryDocumentRepository();
        repository.Documents[DocumentId] = PublishedDocument();
        var cache = new RecordingCacheInvalidationClient();
        var service = new DocumentLifecycleService(repository, cacheInvalidationClient: cache);

        await service.UpdateDraftAsync(
            new UpdateDraftCommand(
                DocumentId,
                "Updated safety policy",
                "Policy",
                "All staff",
                "<p>Updated content</p>",
                GroupRules(OperationsGroupId),
                ActorId,
                "request-edit-cache"),
            CancellationToken.None);

        cache.Calls.Single().Single().Should().Be(DocumentId);
    }

    [Fact]
    public async Task UpdateDraftAsync_OnFreshDraftDoesNotInvalidateSemanticCache()
    {
        var repository = new InMemoryDocumentRepository();
        repository.Documents[DocumentId] = ValidDraft();
        var cache = new RecordingCacheInvalidationClient();
        var service = new DocumentLifecycleService(repository, cacheInvalidationClient: cache);

        await service.UpdateDraftAsync(
            new UpdateDraftCommand(
                DocumentId,
                "Updated draft",
                "Policy",
                "All staff",
                "<p>Updated content</p>",
                GroupRules(OperationsGroupId),
                ActorId,
                "request-edit-fresh"),
            CancellationToken.None);

        cache.Calls.Should().BeEmpty();
    }

    private static DocumentAggregate ValidDraft()
    {
        return DocumentAggregate.NewDraft(
            DocumentId,
            VersionId,
            "Safety policy",
            "Policy",
            "All staff",
            "<p>Wear protective equipment.</p>",
            [Rule(EmpresaUnitId, OperationsGroupId)],
            ActorId);
    }

    private static DocumentAggregate ValidInReview(IReadOnlyList<DocumentAccessRuleRecord>? accessRules = null)
    {
        var draft = ValidDraft();
        if (accessRules is not null)
        {
            draft = draft with { AccessRules = accessRules };
        }

        return draft with
        {
            State = DocumentState.InReview,
            CurrentDraftVersion = draft.CurrentDraftVersion! with
            {
                State = DocumentVersionState.InReview,
                SubmittedForReviewAt = DateTimeOffset.UtcNow,
                SubmittedForReviewByUserId = ActorId,
            },
        };
    }

    private static DocumentAggregate PublishedDocument()
    {
        var publishedVersion = new DocumentVersionRecord(
            VersionId,
            DocumentId,
            1,
            DocumentVersionState.Published,
            "Safety policy",
            "Policy",
            "All staff",
            "<p>Wear protective equipment.</p>",
            DateTimeOffset.UtcNow,
            null,
            null,
            DateTimeOffset.UtcNow,
            ActorId,
            IndexingStatus.Succeeded,
            null);

        return new DocumentAggregate(
            DocumentId,
            "Safety policy",
            DocumentState.Published,
            null,
            publishedVersion,
            [Rule(EmpresaUnitId, OperationsGroupId)],
            ActorId,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private static IReadOnlyList<DocumentAccessRuleDraft> GroupRules(params Guid[] groupIds)
    {
        return groupIds.Select(groupId => new DocumentAccessRuleDraft(null, [groupId])).ToArray();
    }

    private static DocumentAccessRuleRecord Rule(Guid? organizationalUnitId, params Guid[] groupIds)
    {
        return new DocumentAccessRuleRecord(Guid.NewGuid(), organizationalUnitId, groupIds.Distinct().Order().ToArray());
    }

    private static EffectiveAccessScope Scope(
        string primaryRole,
        Guid organizationalUnitId,
        IReadOnlyList<Guid> groupIds)
    {
        return new EffectiveAccessScope(
            ActorId,
            primaryRole,
            primaryRole.Equals("Admin", StringComparison.Ordinal),
            organizationalUnitId,
            groupIds,
            1,
            "published");
    }

    private static InMemoryDocumentAccessPolicyDataSource HierarchyAccessDataSource()
    {
        var source = new InMemoryDocumentAccessPolicyDataSource();
        source.AddClosure(EmpresaUnitId, EmpresaUnitId);
        source.AddClosure(ComunicacionUnitId, ComunicacionUnitId);
        source.AddClosure(MarketingUnitId, MarketingUnitId);
        source.AddClosure(SistemasUnitId, SistemasUnitId);
        source.AddClosure(EmpresaUnitId, ComunicacionUnitId);
        source.AddClosure(EmpresaUnitId, MarketingUnitId);
        source.AddClosure(EmpresaUnitId, SistemasUnitId);
        source.AddClosure(ComunicacionUnitId, MarketingUnitId);
        return source;
    }

    private sealed class InMemoryDocumentRepository : IDocumentRepository
    {
        public Dictionary<Guid, DocumentAggregate> Documents { get; } = [];
        public List<ReviewCommentRecord> ReviewComments { get; } = [];
        public List<DocumentAuditEvent> AuditEvents { get; } = [];

        public Task<IReadOnlyList<DocumentSummary>> ListAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<DocumentSummary>>(
                Documents.Values.Select(DocumentSummary.FromAggregate).ToArray());
        }

        public Task<DocumentAggregate?> FindAsync(Guid documentId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Documents.GetValueOrDefault(documentId));
        }

        public Task SaveAsync(
            DocumentAggregate document,
            IReadOnlyList<ReviewCommentRecord> comments,
            IReadOnlyList<DocumentAuditEvent> auditEvents,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Documents[document.Id] = document;
            ReviewComments.AddRange(comments);
            AuditEvents.AddRange(auditEvents);
            return Task.CompletedTask;
        }
    }

    private sealed class StubHtmlSanitizer : IDocumentHtmlSanitizer
    {
        public string Sanitize(string html)
        {
            return html.Replace("<script>alert(1)</script>", string.Empty, StringComparison.Ordinal);
        }
    }

    private sealed class RecordingIndexingClient : IInternalIndexingClient
    {
        public Guid JobId { get; } = Guid.Parse("55555555-5555-5555-5555-555555555555");

        public List<InternalIndexingRequest> Requests { get; } = [];

        public Task<InternalIndexingResult> CreateIndexingJobAsync(
            InternalIndexingRequest request,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(new InternalIndexingResult(JobId, "Succeeded", 3, null, null));
        }
    }

    private sealed class RecordingCacheInvalidationClient : IInternalCacheInvalidationClient
    {
        public List<IReadOnlyList<Guid>> Calls { get; } = [];

        public Task<int> InvalidateDocumentsAsync(IReadOnlyList<Guid> documentIds, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Calls.Add(documentIds);
            return Task.FromResult(documentIds.Count);
        }
    }

    private sealed class FailingIndexingClient : IInternalIndexingClient
    {
        public Task<InternalIndexingResult> CreateIndexingJobAsync(
            InternalIndexingRequest request,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(new InternalIndexingResult(
                Guid.Parse("66666666-6666-6666-6666-666666666666"),
                "Failed",
                0,
                "INDEXING_NO_CONTENT",
                "Indexing failed."));
        }
    }

    private sealed class InMemoryEffectiveAccessScopeRepository : IEffectiveAccessScopeRepository
    {
        public Dictionary<Guid, EffectiveAccessScope> Scopes { get; } = [];

        public Task<EffectiveAccessScope?> FindForActiveUserAsync(Guid userId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Scopes.GetValueOrDefault(userId));
        }
    }

    private sealed class InMemoryDocumentAccessPolicyDataSource : IDocumentAccessPolicyDataSource
    {
        private readonly HashSet<(Guid AncestorId, Guid DescendantId)> _closures = [];

        public Dictionary<Guid, DocumentAccessGroupPolicy> GroupPolicies { get; } = [];

        public void AddClosure(Guid ancestorId, Guid descendantId)
        {
            _closures.Add((ancestorId, descendantId));
        }

        public Task<bool> IsSameBranchAsync(
            Guid firstOrganizationalUnitId,
            Guid secondOrganizationalUnitId,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(
                _closures.Contains((firstOrganizationalUnitId, secondOrganizationalUnitId))
                || _closures.Contains((secondOrganizationalUnitId, firstOrganizationalUnitId)));
        }

        public Task<bool> IsDescendantOrSelfAsync(
            Guid ancestorOrganizationalUnitId,
            Guid descendantOrganizationalUnitId,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(_closures.Contains((ancestorOrganizationalUnitId, descendantOrganizationalUnitId)));
        }

        public Task<IReadOnlyList<DocumentAccessGroupPolicy>> GetGroupPoliciesAsync(
            Guid publisherUserId,
            IReadOnlyList<Guid> groupIds,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<DocumentAccessGroupPolicy>>(
                groupIds
                    .Where(GroupPolicies.ContainsKey)
                    .Select(groupId => GroupPolicies[groupId])
                    .ToArray());
        }
    }
}
