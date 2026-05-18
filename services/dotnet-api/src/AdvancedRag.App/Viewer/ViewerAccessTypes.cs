namespace AdvancedRag.App.Viewer;

public interface IViewerAccessService
{
    Task<ViewerLinkResult> CreateLinkAsync(CreateViewerLinkCommand command, CancellationToken ct);

    Task<ViewerExchangeResult> ExchangeCodeAsync(ExchangeViewerCodeCommand command, CancellationToken ct);

    Task<ViewerDocumentResult> GetDocumentAsync(GetViewerDocumentCommand command, CancellationToken ct);
}

public interface IViewerAccessRepository
{
    Task<ViewerInstructionAccess?> FindInstructionAsync(Guid instructionId, CancellationToken ct);

    Task SaveExchangeCodeAsync(ViewerExchangeCodeRecord code, CancellationToken ct);

    Task<ViewerExchangeCodeRecord?> FindExchangeCodeByHashAsync(string codeHash, CancellationToken ct);

    Task MarkExchangeCodeConsumedAsync(Guid exchangeCodeId, DateTimeOffset consumedAt, CancellationToken ct);

    Task SaveTokenAuditAsync(ViewerTokenAuditRecord audit, CancellationToken ct);
}

public interface IViewerTokenService
{
    IssuedViewerToken Issue(ViewerTokenIssueRequest request);

    ViewerTokenClaims Validate(string token);
}

public sealed record CreateViewerLinkCommand(
    Guid InstructionId,
    Guid UserId,
    IReadOnlyList<string> Roles,
    string Purpose);

public sealed record ViewerLinkResult(string Url, DateTimeOffset ExpiresAt);

public sealed record ExchangeViewerCodeCommand(string Code);

public sealed record ViewerExchangeResult(
    string Token,
    string ViewerTokenId,
    Guid InstructionId,
    Guid UserId,
    string Purpose,
    DateTimeOffset ExpiresAt);

public sealed record GetViewerDocumentCommand(string Token);

public sealed record ViewerInstructionAccess(
    Guid InstructionId,
    string Title,
    string State,
    ViewerInstructionVersion? DraftVersion,
    ViewerInstructionVersion? PublishedVersion);

public sealed record ViewerInstructionVersion(
    Guid Id,
    int VersionNumber,
    string State,
    string Title,
    string InstructionType,
    string Audience,
    string ContentHtml);

public sealed record ViewerExchangeCodeRecord(
    Guid Id,
    string CodeHash,
    Guid InstructionId,
    Guid UserId,
    string Purpose,
    string AllowedStatuses,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? ConsumedAt,
    DateTimeOffset CreatedAt);

public sealed record ViewerTokenAuditRecord(
    Guid Id,
    string ViewerTokenId,
    Guid InstructionId,
    Guid UserId,
    string Purpose,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);

public sealed record ViewerTokenIssueRequest(
    Guid InstructionId,
    Guid UserId,
    string Purpose,
    IReadOnlyList<string> AllowedStatuses,
    DateTimeOffset ExpiresAt);

public sealed record IssuedViewerToken(
    string Token,
    string ViewerTokenId,
    Guid InstructionId,
    Guid UserId,
    string Purpose,
    DateTimeOffset ExpiresAt);

public sealed record ViewerTokenClaims(
    string ViewerTokenId,
    Guid InstructionId,
    Guid UserId,
    string Purpose,
    IReadOnlyList<string> AllowedStatuses,
    DateTimeOffset ExpiresAt);

public sealed record ViewerDocumentResult(
    Guid InstructionId,
    Guid InstructionVersionId,
    string Title,
    string State,
    string InstructionType,
    string Audience,
    string ContentHtml,
    DateTimeOffset TokenExpiresAt);

public sealed class ViewerAccessException : Exception
{
    public ViewerAccessException(
        string code,
        int httpStatus,
        string message,
        IReadOnlyDictionary<string, object?>? details = null)
        : base(message)
    {
        Code = code;
        HttpStatus = httpStatus;
        Details = details;
    }

    public string Code { get; }

    public int HttpStatus { get; }

    public IReadOnlyDictionary<string, object?>? Details { get; }
}
