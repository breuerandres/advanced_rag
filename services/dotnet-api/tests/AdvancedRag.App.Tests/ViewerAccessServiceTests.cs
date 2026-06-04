using AdvancedRag.App.Viewer;
using FluentAssertions;

namespace AdvancedRag.App.Tests;

public sealed class ViewerAccessServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PublishedDocumentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid DraftDocumentId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset Now = new(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateLink_ForChatReturnsHandoffUrlAndPersistsOneTimeCodeForPublishedDocuments()
    {
        InMemoryViewerAccessRepository repository = SeedRepository();
        InMemoryViewerSessionHandoffRepository handoffs = new();
        FakeTimeProvider clock = new(Now);
        ViewerAccessService service = new(repository, handoffs, timeProvider: clock);

        ViewerLinkResult result = await service.CreateLinkAsync(
            new CreateViewerLinkCommand(PublishedDocumentId, UserId, ["Viewer"], "chat"),
            CancellationToken.None);

        Uri url = new(result.Url);
        Dictionary<string, string> query = ParseQuery(url.Query);
        query["documentId"].Should().Be(PublishedDocumentId.ToString());
        query["handoff"].Should().NotBeNullOrWhiteSpace();
        result.ExpiresAt.Should().Be(Now.AddSeconds(60));
        handoffs.Records.Should().ContainSingle();
        ViewerSessionHandoffRecord record = handoffs.Records.Single();
        record.UserId.Should().Be(UserId);
        record.DocumentId.Should().Be(PublishedDocumentId);
        record.Purpose.Should().Be("chat");
        record.AllowedStateScope.Should().Be("Published");
        record.ExpiresAt.Should().Be(Now.AddSeconds(60));
        record.ConsumedAt.Should().BeNull();
        record.CodeHash.Should().NotBe(query["handoff"]);
        record.CodeHash.Should().HaveLength(64);
    }

    [Fact]
    public async Task CreateLink_ForChatRejectsDraftDocuments()
    {
        ViewerAccessService service = CreateService();

        Func<Task> act = () => service.CreateLinkAsync(
            new CreateViewerLinkCommand(DraftDocumentId, UserId, ["Viewer"], "chat"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ViewerAccessException>().Where(error => error.Code == "AUTH_FORBIDDEN");
    }

    [Fact]
    public async Task CreateLink_ForManagementAllowsDraftDocumentsForDocumentManagers()
    {
        InMemoryViewerAccessRepository repository = SeedRepository();
        ViewerAccessService service = new(repository, new InMemoryViewerSessionHandoffRepository(), timeProvider: new FakeTimeProvider(Now));

        ViewerLinkResult result = await service.CreateLinkAsync(
            new CreateViewerLinkCommand(DraftDocumentId, UserId, ["DocumentManager"], "management"),
            CancellationToken.None);

        result.Url.Should().Contain($"documentId={DraftDocumentId}");
        result.Url.Should().Contain("handoff=");
        result.ExpiresAt.Should().Be(Now.AddSeconds(60));
    }

    [Fact]
    public async Task GetDocument_WithSessionUserReturnsPublishedDocument()
    {
        InMemoryViewerAccessRepository repository = SeedRepository();
        ViewerAccessService service = new(repository, new InMemoryViewerSessionHandoffRepository(), timeProvider: new FakeTimeProvider(Now));

        ViewerDocumentResult first = await service.GetDocumentAsync(
            new GetViewerDocumentCommand(PublishedDocumentId, UserId, ["Viewer"]),
            CancellationToken.None);
        ViewerDocumentResult second = await service.GetDocumentAsync(
            new GetViewerDocumentCommand(PublishedDocumentId, UserId, ["Viewer"]),
            CancellationToken.None);

        first.Title.Should().Be("Published procedure");
        second.DocumentId.Should().Be(first.DocumentId);
    }

    [Fact]
    public async Task ConsumeHandoff_WithFreshCodeMarksItUsedAndReturnsUserScope()
    {
        InMemoryViewerSessionHandoffRepository handoffs = new();
        ViewerAccessService service = new(SeedRepository(), handoffs, timeProvider: new FakeTimeProvider(Now));
        ViewerLinkResult link = await service.CreateLinkAsync(
            new CreateViewerLinkCommand(PublishedDocumentId, UserId, ["Viewer"], "chat"),
            CancellationToken.None);
        string handoffCode = ParseQuery(new Uri(link.Url).Query)["handoff"];

        ViewerSessionHandoffResult result = await service.ConsumeHandoffAsync(
            new ConsumeViewerSessionHandoffCommand(handoffCode, PublishedDocumentId),
            CancellationToken.None);

        result.UserId.Should().Be(UserId);
        result.AllowedStatuses.Should().Equal("Published");
        result.ExpiresAt.Should().Be(Now.AddSeconds(60));
        handoffs.Records.Single().ConsumedAt.Should().Be(Now);
    }

    [Fact]
    public async Task ConsumeHandoff_RejectsExpiredCodes()
    {
        InMemoryViewerSessionHandoffRepository handoffs = new();
        FakeTimeProvider clock = new(Now);
        ViewerAccessService service = new(SeedRepository(), handoffs, timeProvider: clock);
        ViewerLinkResult link = await service.CreateLinkAsync(
            new CreateViewerLinkCommand(PublishedDocumentId, UserId, ["Viewer"], "chat"),
            CancellationToken.None);
        string handoffCode = ParseQuery(new Uri(link.Url).Query)["handoff"];
        clock.Advance(TimeSpan.FromSeconds(61));

        Func<Task> act = () => service.ConsumeHandoffAsync(
            new ConsumeViewerSessionHandoffCommand(handoffCode, PublishedDocumentId),
            CancellationToken.None);

        await act.Should().ThrowAsync<ViewerAccessException>().Where(error => error.Code == "VIEWER_HANDOFF_EXPIRED");
    }

    [Fact]
    public async Task ConsumeHandoff_RejectsAlreadyUsedCodes()
    {
        InMemoryViewerSessionHandoffRepository handoffs = new();
        ViewerAccessService service = new(SeedRepository(), handoffs, timeProvider: new FakeTimeProvider(Now));
        ViewerLinkResult link = await service.CreateLinkAsync(
            new CreateViewerLinkCommand(PublishedDocumentId, UserId, ["Viewer"], "chat"),
            CancellationToken.None);
        string handoffCode = ParseQuery(new Uri(link.Url).Query)["handoff"];
        await service.ConsumeHandoffAsync(
            new ConsumeViewerSessionHandoffCommand(handoffCode, PublishedDocumentId),
            CancellationToken.None);

        Func<Task> act = () => service.ConsumeHandoffAsync(
            new ConsumeViewerSessionHandoffCommand(handoffCode, PublishedDocumentId),
            CancellationToken.None);

        await act.Should().ThrowAsync<ViewerAccessException>().Where(error => error.Code == "VIEWER_HANDOFF_USED");
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

    private static ViewerAccessService CreateService()
    {
        return new ViewerAccessService(SeedRepository(), new InMemoryViewerSessionHandoffRepository(), timeProvider: new FakeTimeProvider(Now));
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        return query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                parts => Uri.UnescapeDataString(parts[0]),
                parts => Uri.UnescapeDataString(parts.Length == 2 ? parts[1] : string.Empty),
                StringComparer.Ordinal);
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

    private sealed class InMemoryViewerSessionHandoffRepository : IViewerSessionHandoffRepository
    {
        public List<ViewerSessionHandoffRecord> Records { get; } = [];

        public Task StoreAsync(ViewerSessionHandoffRecord record, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Records.Add(record);
            return Task.CompletedTask;
        }

        public Task<ViewerSessionHandoffRecord?> FindByCodeHashAsync(string codeHash, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Records.SingleOrDefault(record => record.CodeHash == codeHash));
        }

        public Task MarkConsumedAsync(Guid id, DateTimeOffset consumedAt, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            ViewerSessionHandoffRecord record = Records.Single(item => item.Id == id);
            Records[Records.IndexOf(record)] = record with { ConsumedAt = consumedAt };
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        public void Advance(TimeSpan duration)
        {
            _utcNow = _utcNow.Add(duration);
        }
    }
}
