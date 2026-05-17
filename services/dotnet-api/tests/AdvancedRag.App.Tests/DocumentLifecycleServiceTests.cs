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

        document.State.Should().Be(InstructionState.InReview);
        document.CurrentDraftVersion!.State.Should().Be(InstructionVersionState.InReview);
        repository.ReviewComments.Should().ContainSingle(comment =>
            comment.InstructionVersionId == VersionId && comment.Comment == "Ready for review");
        repository.AuditEvents.Should().ContainSingle(audit =>
            audit.EventType == "instruction.send_to_review" && audit.RequestId == "request-2");
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
    public async Task RequestPublishAsync_AdminMarksVersionAsIndexingPending()
    {
        var repository = new InMemoryDocumentRepository();
        repository.Documents[DocumentId] = ValidInReview();
        var service = new DocumentLifecycleService(repository);

        var document = await service.RequestPublishAsync(
            new RequestPublishCommand(DocumentId, ActorId, ["Admin"], "request-4"),
            CancellationToken.None);

        document.State.Should().Be(InstructionState.InReview);
        document.CurrentDraftVersion!.IndexingStatus.Should().Be(IndexingStatus.Pending);
        repository.AuditEvents.Should().ContainSingle(audit =>
            audit.EventType == "instruction.publish_requested");
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

        document.State.Should().Be(InstructionState.Draft);
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
    public async Task ArchiveAsync_DocumentManagerCannotArchiveActivePublishedInstruction()
    {
        var repository = new InMemoryDocumentRepository();
        repository.Documents[DocumentId] = PublishedDocument();
        var service = new DocumentLifecycleService(repository);

        var act = () => service.ArchiveAsync(
            new ArchiveInstructionCommand(DocumentId, ActorId, ["DocumentManager"], "request-7"),
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<DocumentLifecycleException>()
            .Where(error => error.Code == "AUTH_FORBIDDEN");
    }

    [Fact]
    public async Task RestoreAsync_ArchivedInstructionReturnsToDraftWithoutReactivatingPublishedVersion()
    {
        var repository = new InMemoryDocumentRepository();
        var archived = PublishedDocument() with { State = InstructionState.Archived };
        repository.Documents[DocumentId] = archived;
        var service = new DocumentLifecycleService(repository);

        var document = await service.RestoreAsync(
            new RestoreInstructionCommand(DocumentId, ActorId, "request-8"),
            CancellationToken.None);

        document.State.Should().Be(InstructionState.Draft);
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
            State = InstructionState.InReview,
            CurrentDraftVersion = draft.CurrentDraftVersion! with
            {
                State = InstructionVersionState.InReview,
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
            InstructionVersionState.Published,
            "Safety policy",
            "Policy",
            "All staff",
            "<p>Wear protective equipment.</p>",
            DateTimeOffset.UtcNow,
            null,
            null,
            DateTimeOffset.UtcNow,
            ActorId,
            IndexingStatus.Succeeded);

        return new DocumentAggregate(
            DocumentId,
            "Safety policy",
            InstructionState.Published,
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

        public Task<DocumentAggregate?> FindAsync(Guid instructionId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Documents.GetValueOrDefault(instructionId));
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

    private sealed class StubHtmlSanitizer : IInstructionHtmlSanitizer
    {
        public string Sanitize(string html)
        {
            return html.Replace("<script>alert(1)</script>", string.Empty, StringComparison.Ordinal);
        }
    }
}
