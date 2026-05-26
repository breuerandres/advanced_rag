using AdvancedRag.App.Viewer;

namespace AdvancedRag.Api.Models.Viewer;

public sealed record ViewerLinkResponse(string Url, DateTimeOffset ExpiresAt)
{
    public static ViewerLinkResponse FromResult(ViewerLinkResult result)
    {
        return new ViewerLinkResponse(result.Url, result.ExpiresAt);
    }
}

public sealed record ViewerDocumentResponse(
    Guid DocumentId,
    Guid DocumentVersionId,
    string Title,
    string State,
    string DocumentType,
    string Audience,
    string ContentHtml,
    DateTimeOffset TokenExpiresAt)
{
    public static ViewerDocumentResponse FromResult(ViewerDocumentResult result)
    {
        return new ViewerDocumentResponse(
            result.DocumentId,
            result.DocumentVersionId,
            result.Title,
            result.State,
            result.DocumentType,
            result.Audience,
            result.ContentHtml,
            result.TokenExpiresAt);
    }
}

public sealed record ViewerDocumentCatalogResponse(
    IReadOnlyList<ViewerDocumentCatalogItemResponse> Documents,
    IReadOnlyList<ViewerDocumentGroupResponse> Groups)
{
    public static ViewerDocumentCatalogResponse FromCatalog(ViewerDocumentCatalog catalog)
    {
        return new ViewerDocumentCatalogResponse(
            catalog.Documents.Select(ViewerDocumentCatalogItemResponse.FromItem).ToArray(),
            catalog.Groups.Select(group => new ViewerDocumentGroupResponse(group.Id, group.Name)).ToArray());
    }
}

public sealed record ViewerDocumentCatalogItemResponse(
    Guid Id,
    string Title,
    string State,
    string DocumentType,
    string Audience,
    IReadOnlyList<ViewerDocumentGroupResponse> AllowedGroups,
    DateTimeOffset UpdatedAt)
{
    public static ViewerDocumentCatalogItemResponse FromItem(ViewerDocumentCatalogItem item)
    {
        return new ViewerDocumentCatalogItemResponse(
            item.Id,
            item.Title,
            item.State,
            item.DocumentType,
            item.Audience,
            item.AllowedGroups.Select(group => new ViewerDocumentGroupResponse(group.Id, group.Name)).ToArray(),
            item.UpdatedAt);
    }
}

public sealed record ViewerDocumentGroupResponse(Guid Id, string Name);
