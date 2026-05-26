using AdvancedRag.App.Viewer;
using FluentAssertions;

namespace AdvancedRag.App.Tests;

public sealed class ViewerAccessServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PublishedDocumentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid DraftDocumentId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task CreateLink_ForChatReturnsDirectSessionDocumentUrlForPublishedDocuments()
    {
        InMemoryViewerAccessRepository repository = SeedRepository();
        ViewerAccessService service = new(repository);

        ViewerLinkResult result = await service.CreateLinkAsync(
            new CreateViewerLinkCommand(PublishedDocumentId, UserId, ["Viewer"], "chat"),
            CancellationToken.None);

        result.Url.Should().Be($"https://docs.client.com/open?documentId={PublishedDocumentId}");
        result.ExpiresAt.Should().Be(DateTimeOffset.MaxValue);
    }

    [Fact]
    public async Task CreateLink_ForChatRejectsDraftDocuments()
    {
        ViewerAccessService service = new(SeedRepository());

        Func<Task> act = () => service.CreateLinkAsync(
            new CreateViewerLinkCommand(DraftDocumentId, UserId, ["Viewer"], "chat"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ViewerAccessException>().Where(error => error.Code == "AUTH_FORBIDDEN");
    }

    [Fact]
    public async Task CreateLink_ForManagementAllowsDraftDocumentsForDocumentManagers()
    {
        InMemoryViewerAccessRepository repository = SeedRepository();
        ViewerAccessService service = new(repository);

        ViewerLinkResult result = await service.CreateLinkAsync(
            new CreateViewerLinkCommand(DraftDocumentId, UserId, ["DocumentManager"], "management"),
            CancellationToken.None);

        result.Url.Should().Be($"https://docs.client.com/open?documentId={DraftDocumentId}");
    }

    [Fact]
    public async Task GetDocument_WithSessionUserReturnsPublishedDocument()
    {
        InMemoryViewerAccessRepository repository = SeedRepository();
        ViewerAccessService service = new(repository);

        ViewerDocumentResult first = await service.GetDocumentAsync(
            new GetViewerDocumentCommand(PublishedDocumentId, UserId, ["Viewer"]),
            CancellationToken.None);
        ViewerDocumentResult second = await service.GetDocumentAsync(
            new GetViewerDocumentCommand(PublishedDocumentId, UserId, ["Viewer"]),
            CancellationToken.None);

        first.Title.Should().Be("Published procedure");
        second.DocumentId.Should().Be(first.DocumentId);
    }

    private static InMemoryViewerAccessRepository SeedRepository()
    {
        InMemoryViewerAccessRepository repository = new();
        repository.Documents[PublishedDocumentId] = new ViewerDocumentAccess(
            PublishedDocumentId,
            "Published procedure",
            "Published",
            null,
            new ViewerDocumentVersion(
                Guid.Parse("44444444-4444-4444-4444-444444444444"),
                1,
                "Published",
                "Published procedure",
                "Policy",
                "Operations",
                "<p>Contenido publicado</p>"));
        repository.Documents[DraftDocumentId] = new ViewerDocumentAccess(
            DraftDocumentId,
            "Draft procedure",
            "Draft",
            new ViewerDocumentVersion(
                Guid.Parse("55555555-5555-5555-5555-555555555555"),
                1,
                "Draft",
                "Draft procedure",
                "Policy",
                "Operations",
                "<p>Borrador</p>"),
            null);
        return repository;
    }

    private sealed class InMemoryViewerAccessRepository : IViewerAccessRepository
    {
        public Dictionary<Guid, ViewerDocumentAccess> Documents { get; } = [];

        public Task<ViewerDocumentAccess?> FindDocumentAsync(Guid documentId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Documents.GetValueOrDefault(documentId));
        }
    }
}
