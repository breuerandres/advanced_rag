using AdvancedRag.App.Viewer;
using FluentAssertions;

namespace AdvancedRag.App.Tests;

public sealed class ViewerAccessServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PublishedDocumentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid DraftDocumentId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task CreateLink_ForChatPersistsSixtySecondSingleUseCodeForPublishedDocuments()
    {
        InMemoryViewerAccessRepository repository = SeedRepository();
        RecordingViewerTokenService tokenService = new();
        ViewerAccessService service = new(repository, tokenService);

        ViewerLinkResult result = await service.CreateLinkAsync(
            new CreateViewerLinkCommand(PublishedDocumentId, UserId, ["Viewer"], "chat"),
            CancellationToken.None);

        result.Url.Should().StartWith("https://docs.client.com/open?code=");
        repository.ExchangeCodes.Should().ContainSingle();
        ViewerExchangeCodeRecord code = repository.ExchangeCodes.Single();
        code.Purpose.Should().Be("chat");
        code.AllowedStatuses.Should().Be("Published");
        (code.ExpiresAt - code.CreatedAt).Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task CreateLink_ForChatRejectsDraftDocuments()
    {
        ViewerAccessService service = new(SeedRepository(), new RecordingViewerTokenService());

        Func<Task> act = () => service.CreateLinkAsync(
            new CreateViewerLinkCommand(DraftDocumentId, UserId, ["Viewer"], "chat"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ViewerAccessException>().Where(error => error.Code == "AUTH_FORBIDDEN");
    }

    [Fact]
    public async Task CreateLink_ForManagementAllowsDraftDocumentsForDocumentManagers()
    {
        InMemoryViewerAccessRepository repository = SeedRepository();
        ViewerAccessService service = new(repository, new RecordingViewerTokenService());

        await service.CreateLinkAsync(
            new CreateViewerLinkCommand(DraftDocumentId, UserId, ["DocumentManager"], "management"),
            CancellationToken.None);

        repository.ExchangeCodes.Single().AllowedStatuses.Should().Be("Draft,In Review,Published");
    }

    [Fact]
    public async Task ExchangeCode_MarksCodeConsumedAndAuditsViewerToken()
    {
        InMemoryViewerAccessRepository repository = SeedRepository();
        RecordingViewerTokenService tokenService = new();
        ViewerAccessService service = new(repository, tokenService);
        ViewerLinkResult link = await service.CreateLinkAsync(
            new CreateViewerLinkCommand(PublishedDocumentId, UserId, ["Viewer"], "chat"),
            CancellationToken.None);
        string code = new Uri(link.Url).Query.Split("code=", StringSplitOptions.None)[1];

        ViewerExchangeResult result = await service.ExchangeCodeAsync(
            new ExchangeViewerCodeCommand(code),
            CancellationToken.None);

        result.Token.Should().Be("viewer-token");
        repository.ExchangeCodes.Single().ConsumedAt.Should().NotBeNull();
        repository.TokenAudit.Should().ContainSingle(audit =>
            audit.ViewerTokenId == "viewer-token-id" && audit.DocumentId == PublishedDocumentId);
    }

    [Fact]
    public async Task ExchangeCode_RejectsAlreadyUsedCode()
    {
        InMemoryViewerAccessRepository repository = SeedRepository();
        ViewerAccessService service = new(repository, new RecordingViewerTokenService());
        ViewerLinkResult link = await service.CreateLinkAsync(
            new CreateViewerLinkCommand(PublishedDocumentId, UserId, ["Viewer"], "chat"),
            CancellationToken.None);
        string code = new Uri(link.Url).Query.Split("code=", StringSplitOptions.None)[1];
        await service.ExchangeCodeAsync(new ExchangeViewerCodeCommand(code), CancellationToken.None);

        Func<Task> act = () => service.ExchangeCodeAsync(new ExchangeViewerCodeCommand(code), CancellationToken.None);

        await act.Should().ThrowAsync<ViewerAccessException>().Where(error => error.Code == "VIEWER_CODE_USED");
    }

    [Fact]
    public async Task GetDocument_WithReusableViewerTokenReturnsPublishedDocument()
    {
        InMemoryViewerAccessRepository repository = SeedRepository();
        ViewerAccessService service = new(repository, new RecordingViewerTokenService());

        ViewerDocumentResult first = await service.GetDocumentAsync(
            new GetViewerDocumentCommand("viewer-token"),
            CancellationToken.None);
        ViewerDocumentResult second = await service.GetDocumentAsync(
            new GetViewerDocumentCommand("viewer-token"),
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
        public List<ViewerExchangeCodeRecord> ExchangeCodes { get; } = [];
        public List<ViewerTokenAuditRecord> TokenAudit { get; } = [];

        public Task<ViewerDocumentAccess?> FindDocumentAsync(Guid documentId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Documents.GetValueOrDefault(documentId));
        }

        public Task SaveExchangeCodeAsync(ViewerExchangeCodeRecord code, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            ExchangeCodes.Add(code);
            return Task.CompletedTask;
        }

        public Task<ViewerExchangeCodeRecord?> FindExchangeCodeByHashAsync(string codeHash, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(ExchangeCodes.SingleOrDefault(code => code.CodeHash == codeHash));
        }

        public Task MarkExchangeCodeConsumedAsync(Guid exchangeCodeId, DateTimeOffset consumedAt, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            int index = ExchangeCodes.FindIndex(code => code.Id == exchangeCodeId);
            ExchangeCodes[index] = ExchangeCodes[index] with { ConsumedAt = consumedAt };
            return Task.CompletedTask;
        }

        public Task SaveTokenAuditAsync(ViewerTokenAuditRecord audit, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            TokenAudit.Add(audit);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingViewerTokenService : IViewerTokenService
    {
        public IssuedViewerToken Issue(ViewerTokenIssueRequest request)
        {
            return new IssuedViewerToken(
                "viewer-token",
                "viewer-token-id",
                request.DocumentId,
                request.UserId,
                request.Purpose,
                request.ExpiresAt);
        }

        public ViewerTokenClaims Validate(string token)
        {
            if (token != "viewer-token")
            {
                throw new ViewerAccessException("AUTH_TOKEN_INVALID", 401, "Viewer token is invalid.");
            }

            return new ViewerTokenClaims(
                "viewer-token-id",
                PublishedDocumentId,
                UserId,
                "chat",
                ["Published"],
                DateTimeOffset.UtcNow.AddMinutes(15));
        }
    }
}
