using AdvancedRag.App.Viewer;

namespace AdvancedRag.Api.Models.Viewer;

public sealed record ViewerLinkResponse(string Url, DateTimeOffset ExpiresAt)
{
    public static ViewerLinkResponse FromResult(ViewerLinkResult result)
    {
        return new ViewerLinkResponse(result.Url, result.ExpiresAt);
    }
}

public sealed record ViewerExchangeResponse(Guid InstructionId, DateTimeOffset ExpiresAt)
{
    public static ViewerExchangeResponse FromResult(ViewerExchangeResult result)
    {
        return new ViewerExchangeResponse(result.InstructionId, result.ExpiresAt);
    }
}

public sealed record ViewerDocumentResponse(
    Guid InstructionId,
    Guid InstructionVersionId,
    string Title,
    string State,
    string InstructionType,
    string Audience,
    string ContentHtml,
    DateTimeOffset TokenExpiresAt)
{
    public static ViewerDocumentResponse FromResult(ViewerDocumentResult result)
    {
        return new ViewerDocumentResponse(
            result.InstructionId,
            result.InstructionVersionId,
            result.Title,
            result.State,
            result.InstructionType,
            result.Audience,
            result.ContentHtml,
            result.TokenExpiresAt);
    }
}
