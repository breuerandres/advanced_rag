namespace AdvancedRag.Api.Models.Documents;

public sealed record SaveDocumentDraftRequest(
    string Title,
    Guid? DocumentTypeId,
    string Audience,
    string ContentHtml,
    IReadOnlyList<Guid>? AllowedGroupIds,
    IReadOnlyList<DocumentAccessRuleRequest>? AccessRules);

public sealed record DocumentAccessRuleRequest(
    Guid? OrganizationalUnitId,
    IReadOnlyList<Guid>? GroupIds);

public sealed record ReviewCommentRequest(string? Comment);
