namespace AdvancedRag.Api.Models.Documents;

public sealed record SaveDocumentDraftRequest(
    string Title,
    string InstructionType,
    string Audience,
    string ContentHtml,
    IReadOnlyList<Guid>? AllowedGroupIds);

public sealed record ReviewCommentRequest(string? Comment);
