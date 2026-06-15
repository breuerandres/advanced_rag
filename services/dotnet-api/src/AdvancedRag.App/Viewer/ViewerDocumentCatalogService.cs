using AdvancedRag.App.Auth;
using AdvancedRag.App.Documents;
using AdvancedRag.App.Users;

namespace AdvancedRag.App.Viewer;

public interface IViewerDocumentCatalogService
{
    Task<ViewerDocumentCatalog> ListAsync(AuthenticatedUser user, CancellationToken ct);
}

public sealed class ViewerDocumentCatalogService : IViewerDocumentCatalogService
{
    private readonly IDocumentRepository _documents;
    private readonly IViewerDocumentGroupSource _groups;
    private readonly IEffectiveAccessScopeRepository? _accessScopes;
    private readonly IDocumentAccessPolicy? _accessPolicy;

    public ViewerDocumentCatalogService(
        IDocumentRepository documents,
        IViewerDocumentGroupSource groups,
        IEffectiveAccessScopeRepository? accessScopes = null,
        IDocumentAccessPolicy? accessPolicy = null)
    {
        _documents = documents;
        _groups = groups;
        _accessScopes = accessScopes;
        _accessPolicy = accessPolicy;
    }

    public async Task<ViewerDocumentCatalog> ListAsync(AuthenticatedUser user, CancellationToken ct)
    {
        IReadOnlyList<DocumentSummary> documents = await _documents.ListAsync(ct);
        IReadOnlyList<GroupRecord> groups = await _groups.ListGroupsAsync(ct);
        Dictionary<Guid, GroupRecord> groupsById = groups.ToDictionary(group => group.Id);
        bool isAdmin = user.Roles.Contains("Admin", StringComparer.Ordinal);
        HashSet<Guid> userGroupIds = user.Groups.Select(group => group.Id).ToHashSet();

        var visibleDocuments = new List<ViewerDocumentCatalogItem>();
        EffectiveAccessScope? scope = _accessScopes is null
            ? null
            : await _accessScopes.FindForActiveUserAsync(user.Id, ct);
        foreach (DocumentSummary document in documents)
        {
            bool isVisible = _accessPolicy is null || scope is null
                ? isAdmin || IsPublishedForUserGroup(document, userGroupIds)
                : await IsVisibleAsync(scope, document, ct);
            if (isVisible)
            {
                visibleDocuments.Add(MapDocument(document, groupsById));
            }
        }

        GroupRecord[] visibleGroups = visibleDocuments
            .SelectMany(document => document.AllowedGroups)
            .DistinctBy(group => group.Id)
            .OrderBy(group => group.Name, StringComparer.Ordinal)
            .ToArray();

        return new ViewerDocumentCatalog(visibleDocuments, visibleGroups);
    }

    private async Task<bool> IsVisibleAsync(EffectiveAccessScope scope, DocumentSummary document, CancellationToken ct)
    {
        if (document.State == DocumentState.Published)
        {
            return await _accessPolicy!.CanReadPublishedDocumentAsync(scope, document.AccessRules, ct);
        }

        return await _accessPolicy!.CanManageDraftAsync(scope, document.AccessRules, ct);
    }

    private static bool IsPublishedForUserGroup(DocumentSummary document, HashSet<Guid> userGroupIds)
    {
        return document.State == DocumentState.Published
            && document.AllowedGroupIds.Any(userGroupIds.Contains);
    }

    private static ViewerDocumentCatalogItem MapDocument(
        DocumentSummary document,
        IReadOnlyDictionary<Guid, GroupRecord> groupsById)
    {
        GroupRecord[] allowedGroups = document.AllowedGroupIds
            .Select(groupId => groupsById.GetValueOrDefault(groupId) ?? new GroupRecord(groupId, "Sin grupo"))
            .OrderBy(group => group.Name, StringComparer.Ordinal)
            .ToArray();

        return new ViewerDocumentCatalogItem(
            document.Id,
            document.Title,
            ToDisplayState(document.State),
            document.DocumentType,
            allowedGroups,
            document.UpdatedAt);
    }

    private static string ToDisplayState(DocumentState state)
    {
        return state switch
        {
            DocumentState.InReview => "In Review",
            _ => state.ToString(),
        };
    }
}

public sealed record ViewerDocumentCatalog(
    IReadOnlyList<ViewerDocumentCatalogItem> Documents,
    IReadOnlyList<GroupRecord> Groups);

public sealed record ViewerDocumentCatalogItem(
    Guid Id,
    string Title,
    string State,
    string DocumentType,
    IReadOnlyList<GroupRecord> AllowedGroups,
    DateTimeOffset UpdatedAt);

public interface IViewerDocumentGroupSource
{
    Task<IReadOnlyList<GroupRecord>> ListGroupsAsync(CancellationToken ct);
}
