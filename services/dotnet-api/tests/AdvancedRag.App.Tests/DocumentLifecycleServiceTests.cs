using AdvancedRag.App.Documents;
using FluentAssertions;

namespace AdvancedRag.App.Tests;

public sealed class DocumentLifecycleServiceTests
{
    private static readonly Guid ActorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DocumentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid VersionId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid OperationsGroupId = Guid.Parse("44444444-4444-4444-4444-444444444444");

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
                [OperationsGroupId],
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
                [OperationsGroupId],
                ActorId,
                "request-sanitize"),
            CancellationToken.None);

        document.CurrentDraftVersion!.ContentHtml.Should().Be("<p>Safe content</p>");
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

    private static DocumentAggregate ValidDraft()
    {
        return DocumentAggregate.NewDraft(
            DocumentId,
            VersionId,
            "Safety policy",
            "Policy",
            "All staff",
            "<p>Wear protective equipment.</p>",
            [OperationsGroupId],
            ActorId);
    }

    private static DocumentAggregate ValidInReview()
    {
        var draft = ValidDraft();
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
            [OperationsGroupId],
            ActorId,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
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
}
