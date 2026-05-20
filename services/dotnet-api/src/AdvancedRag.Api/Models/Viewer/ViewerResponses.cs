using AdvancedRag.App.Viewer;

namespace AdvancedRag.Api.Models.Viewer;

public sealed record ViewerLinkResponse(string Url, DateTimeOffset ExpiresAt)
{
    public static ViewerLinkResponse FromResult(ViewerLinkResult result)
    {
        return new ViewerLinkResponse(result.Url, result.ExpiresAt);
    }
}

public sealed record ViewerExchangeResponse(Guid DocumentId, DateTimeOffset ExpiresAt)
{
    public static ViewerExchangeResponse FromResult(ViewerExchangeResult result)
    {
        return new ViewerExchangeResponse(result.DocumentId, result.ExpiresAt);
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
