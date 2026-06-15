using AdvancedRag.App.Viewer;
using AdvancedRag.App.Auth;
using AdvancedRag.App.Documents;
using FluentAssertions;

namespace AdvancedRag.App.Tests;

public sealed class ViewerAccessServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PublishedDocumentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid DraftDocumentId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid EmpresaUnitId = DocumentAccessPolicy.RootOrganizationalUnitId;
    private static readonly Guid ComunicacionUnitId = Guid.Parse("01000000-0000-0000-0000-000000000002");
    private static readonly Guid MarketingUnitId = Guid.Parse("01000000-0000-0000-0000-000000000003");
    private static readonly Guid AudiovisualUnitId = Guid.Parse("01000000-0000-0000-0000-000000000004");
    private static readonly Guid SistemasUnitId = Guid.Parse("01000000-0000-0000-0000-000000000005");
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

    [Theory]
    [InlineData("viewer.marketing@demo.com", "Calendario de campanas de Marketing", true)]
    [InlineData("viewer.marketing@demo.com", "Checklist de produccion audiovisual", false)]
    [InlineData("viewer.sistemas@demo.com", "Guia del area Comunicacion", false)]
    public async Task BranchScopedReadRules_MatchOnlyExpectedDocuments(
        string userEmail,
        string documentTitle,
        bool expectedAllowed)
    {
        InMemoryViewerAccessRepository repository = SeedHierarchyRepository();
        InMemoryEffectiveAccessScopeRepository scopes = new();
        scopes.Scopes[UserId] = userEmail switch
        {
            "viewer.marketing@demo.com" => Scope("Viewer", MarketingUnitId, []),
            "viewer.sistemas@demo.com" => Scope("Viewer", SistemasUnitId, []),
            _ => throw new InvalidOperationException("Unknown user."),
        };
        ViewerAccessService service = new(
            repository,
            new InMemoryViewerSessionHandoffRepository(),
            timeProvider: new FakeTimeProvider(Now),
            accessScopes: scopes,
            accessPolicy: new DocumentAccessPolicy(HierarchyAccessDataSource()));
        Guid documentId = repository.Documents.Single(document => document.Value.Title == documentTitle).Key;

        Func<Task<ViewerDocumentResult>> act = () => service.GetDocumentAsync(
            new GetViewerDocumentCommand(documentId, UserId, ["Viewer"]),
            CancellationToken.None);

        if (expectedAllowed)
        {
            ViewerDocumentResult result = await act();
            result.Title.Should().Be(documentTitle);
        }
        else
        {
            await act.Should()
                .ThrowAsync<ViewerAccessException>()
                .Where(error => error.Code == "AUTH_FORBIDDEN");
        }
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
    public async Task CreateLink_ForManagementAllowsDraftDocumentsForDocumentEditors()
    {
        InMemoryViewerAccessRepository repository = SeedRepository();
        ViewerAccessService service = new(repository, new InMemoryViewerSessionHandoffRepository(), timeProvider: new FakeTimeProvider(Now));

        ViewerLinkResult result = await service.CreateLinkAsync(
            new CreateViewerLinkCommand(DraftDocumentId, UserId, ["DocumentEditor"], "management"),
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
                "<p>Contenido publicado</p>"),
            [Rule(EmpresaUnitId)]);
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
                "<p>Borrador</p>"),
            null,
            [Rule(EmpresaUnitId)]);
        return repository;
    }

    private static InMemoryViewerAccessRepository SeedHierarchyRepository()
    {
        InMemoryViewerAccessRepository repository = new();
        AddPublished(repository, Guid.Parse("60000000-0000-0000-0000-000000000001"), "Guia del area Comunicacion", ComunicacionUnitId);
        AddPublished(repository, Guid.Parse("60000000-0000-0000-0000-000000000002"), "Calendario de campanas de Marketing", MarketingUnitId);
        AddPublished(repository, Guid.Parse("60000000-0000-0000-0000-000000000003"), "Checklist de produccion audiovisual", AudiovisualUnitId);
        AddPublished(repository, Guid.Parse("60000000-0000-0000-0000-000000000004"), "Procedimiento de guardias de Sistemas", SistemasUnitId);
        return repository;
    }

    private static void AddPublished(
        InMemoryViewerAccessRepository repository,
        Guid documentId,
        string title,
        Guid organizationalUnitId)
    {
        repository.Documents[documentId] = new ViewerDocumentAccess(
            documentId,
            title,
            "Published",
            null,
            new ViewerDocumentVersion(
                Guid.NewGuid(),
                1,
                "Published",
                title,
                "Manual",
                "<p>Contenido publicado</p>"),
            [Rule(organizationalUnitId)]);
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
            UserId,
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
        source.AddClosure(AudiovisualUnitId, AudiovisualUnitId);
        source.AddClosure(SistemasUnitId, SistemasUnitId);
        source.AddClosure(EmpresaUnitId, ComunicacionUnitId);
        source.AddClosure(EmpresaUnitId, MarketingUnitId);
        source.AddClosure(EmpresaUnitId, AudiovisualUnitId);
        source.AddClosure(EmpresaUnitId, SistemasUnitId);
        source.AddClosure(ComunicacionUnitId, MarketingUnitId);
        source.AddClosure(ComunicacionUnitId, AudiovisualUnitId);
        return source;
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
            return Task.FromResult<IReadOnlyList<DocumentAccessGroupPolicy>>([]);
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
