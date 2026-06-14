using AdvancedRag.App.DocumentImages;
using AdvancedRag.App.Documents;
using FluentAssertions;

namespace AdvancedRag.App.Tests;

public sealed class DocumentImportServiceTests
{
    private static readonly Guid ActorId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private const string DocxMime =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    [Fact]
    public async Task ImportDocxAsync_CreatesDraftWithFilenameTitleAndPersistsImages()
    {
        var repository = new RecordingDocumentRepository();
        var storage = new RecordingObjectStorage();
        var images = new RecordingImageRepository();
        var extraction = new StubExtractionService();
        var service = new DocumentImportService(extraction, repository, storage, images);

        DocumentAggregate result = await service.ImportDocxAsync(
            new ImportDocxCommand("Quarterly Report.docx", DocxMime, new byte[] { 1, 2, 3 }, ActorId, "req-1"),
            CancellationToken.None);

        result.Title.Should().Be("Quarterly Report");
        result.State.Should().Be(DocumentState.Draft);
        result.AccessRules.Should().BeEmpty();
        result.CurrentDraftVersion!.ContentHtml.Should().Contain("/api/document-images/");
        repository.Saved.Should().NotBeNull();
        storage.Puts.Should().HaveCount(1);
        images.Added.Should().HaveCount(1);
        images.Added[0].DocumentId.Should().Be(result.Id);
        images.Added[0].UploadedByUserId.Should().Be(ActorId);
    }

    [Fact]
    public async Task ImportDocxAsync_BlankFilename_FallsBackToDefaultTitle()
    {
        var service = new DocumentImportService(
            new StubExtractionService(), new RecordingDocumentRepository(),
            new RecordingObjectStorage(), new RecordingImageRepository());

        DocumentAggregate result = await service.ImportDocxAsync(
            new ImportDocxCommand(".docx", DocxMime, new byte[] { 1 }, ActorId, "req-2"),
            CancellationToken.None);

        result.Title.Should().Be("Imported document");
    }

    private sealed class StubExtractionService : IDocumentImportExtractionService
    {
        public Task<ImportExtractionResult> ExtractAsync(ImportExtractionCommand command, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<DocxImportExtractionResult> ExtractDocxWithImagesAsync(
            ImportExtractionCommand command, Guid documentId, CancellationToken ct)
        {
            Guid imageId = Guid.NewGuid();
            var image = new ImportImageContent(
                imageId,
                DocumentImageObjectKey.Build(documentId, imageId, "abc", ".png"),
                "image/png", 3, "abc", "diagram", new byte[] { 9, 9, 9 });
            return Task.FromResult(new DocxImportExtractionResult(
                "Body text",
                $"<p>Body text</p><img src=\"/api/document-images/{imageId:D}/content\" alt=\"diagram\">",
                new ImportExtractionMetadata(null, command.OriginalFilename, command.MimeType, 3, "hash", "Extracted"),
                new[] { image }));
        }
    }

    private sealed class RecordingDocumentRepository : IDocumentRepository
    {
        public DocumentAggregate? Saved { get; private set; }

        public Task<IReadOnlyList<DocumentSummary>> ListAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DocumentSummary>>(Array.Empty<DocumentSummary>());

        public Task<DocumentAggregate?> FindAsync(Guid documentId, CancellationToken ct) =>
            Task.FromResult<DocumentAggregate?>(null);

        public Task SaveAsync(
            DocumentAggregate document,
            IReadOnlyList<ReviewCommentRecord> comments,
            IReadOnlyList<DocumentAuditEvent> auditEvents,
            CancellationToken ct)
        {
            Saved = document;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingObjectStorage : IDocumentImageObjectStorage
    {
        public List<string> Puts { get; } = new();

        public Task PutAsync(string objectKey, string contentType, Stream content, CancellationToken ct)
        {
            Puts.Add(objectKey);
            return Task.CompletedTask;
        }

        public Task<Stream> GetAsync(string objectKey, CancellationToken ct) =>
            Task.FromResult<Stream>(new MemoryStream());
    }

    private sealed class RecordingImageRepository : IDocumentImageRepository
    {
        public List<DocumentImageRecord> Added { get; } = new();

        public Task AddAsync(DocumentImageRecord image, CancellationToken ct)
        {
            Added.Add(image);
            return Task.CompletedTask;
        }

        public Task<DocumentImageRecord?> FindAsync(Guid imageId, CancellationToken ct) =>
            Task.FromResult<DocumentImageRecord?>(null);
    }
}
