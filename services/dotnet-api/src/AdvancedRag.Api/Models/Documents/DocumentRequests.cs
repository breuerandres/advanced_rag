namespace AdvancedRag.Api.Models.Documents;

public sealed record SaveDocumentDraftRequest(
    string Title,
    string DocumentType,
    string Audience,
    string ContentHtml,
    IReadOnlyList<Guid>? AllowedGroupIds,
    IReadOnlyList<DocumentAccessRuleRequest>? AccessRules);

public sealed record DocumentAccessRuleRequest(
    Guid? OrganizationalUnitId,
    IReadOnlyList<Guid>? GroupIds);

public sealed record ReviewCommentRequest(string? Comment);
