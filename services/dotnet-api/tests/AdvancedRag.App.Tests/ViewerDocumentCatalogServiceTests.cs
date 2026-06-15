using AdvancedRag.App.Auth;
using AdvancedRag.App.Documents;
using AdvancedRag.App.Users;
using AdvancedRag.App.Viewer;
using FluentAssertions;

namespace AdvancedRag.App.Tests;

public sealed class ViewerDocumentCatalogServiceTests
{
    private static readonly Guid AdminId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ViewerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid LegalGroupId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid FinanceGroupId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid EmpresaUnitId = DocumentAccessPolicy.RootOrganizationalUnitId;
    private static readonly Guid PublishedLegalDocumentId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid PublishedFinanceDocumentId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid DraftLegalDocumentId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    [Fact]
    public async Task ListAsync_AdminSeesEveryDocumentInCatalog()
    {
        ViewerDocumentCatalogService service = CreateService();

        ViewerDocumentCatalog catalog = await service.ListAsync(
            new AuthenticatedUser(AdminId, "admin@example.com", "Admin", ["Admin"], []),
            CancellationToken.None);

        catalog.Documents.Select(document => document.Id).Should().BeEquivalentTo([
            PublishedLegalDocumentId,
            PublishedFinanceDocumentId,
            DraftLegalDocumentId,
        ]);
    }

    [Fact]
    public async Task ListAsync_ViewerSeesOnlyPublishedDocumentsForTheirGroups()
    {
        ViewerDocumentCatalogService service = CreateService();

        ViewerDocumentCatalog catalog = await service.ListAsync(
            new AuthenticatedUser(
                ViewerId,
                "viewer@example.com",
                "Viewer",
                ["Viewer"],
                [new AuthGroup(LegalGroupId, "Legales")]),
            CancellationToken.None);

        catalog.Documents.Select(document => document.Id).Should().Equal(PublishedLegalDocumentId);
        catalog.Groups.Should().ContainSingle(group => group.Id == LegalGroupId && group.Name == "Legales");
    }

    private static ViewerDocumentCatalogService CreateService()
    {
        return new ViewerDocumentCatalogService(
            new InMemoryDocumentRepository(),
            new InMemoryGroupSource());
    }

    private sealed class InMemoryDocumentRepository : IDocumentRepository
    {
        public Task<IReadOnlyList<DocumentSummary>> ListAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<DocumentSummary>>([
                new(
                    PublishedLegalDocumentId,
                    "Manual legal",
                    DocumentState.Published,
                    null,
                    "Manual",
                    [Rule(EmpresaUnitId, LegalGroupId)],
                    null,
                    1,
                    IndexingStatus.Succeeded,
                    DateTimeOffset.UtcNow),
                new(
                    PublishedFinanceDocumentId,
                    "Manual financiero",
                    DocumentState.Published,
                    null,
                    "Manual",
                    [Rule(EmpresaUnitId, FinanceGroupId)],
                    null,
                    1,
                    IndexingStatus.Succeeded,
                    DateTimeOffset.UtcNow),
                new(
                    DraftLegalDocumentId,
                    "Borrador legal",
                    DocumentState.Draft,
                    null,
                    "Manual",
                    [Rule(EmpresaUnitId, LegalGroupId)],
                    2,
                    1,
                    IndexingStatus.None,
                    DateTimeOffset.UtcNow),
            ]);
        }

        public Task<DocumentAggregate?> FindAsync(Guid documentId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<DocumentAggregate?>(null);
        }

        public Task SaveAsync(
            DocumentAggregate document,
            IReadOnlyList<ReviewCommentRecord> comments,
            IReadOnlyList<DocumentAuditEvent> auditEvents,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryGroupSource : IViewerDocumentGroupSource
    {
        public Task<IReadOnlyList<GroupRecord>> ListGroupsAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<GroupRecord>>([
                new(LegalGroupId, "Legales"),
                new(FinanceGroupId, "Finanzas"),
            ]);
        }
    }

    private static DocumentAccessRuleRecord Rule(Guid? organizationalUnitId, params Guid[] groupIds)
    {
        return new DocumentAccessRuleRecord(Guid.NewGuid(), organizationalUnitId, groupIds.Distinct().Order().ToArray());
    }
}
