using System.Security.Cryptography;
using System.Text;

namespace AdvancedRag.App.Viewer;

public sealed class ViewerAccessService : IViewerAccessService
{
    private static readonly TimeSpan ExchangeCodeTtl = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ViewerTokenTtl = TimeSpan.FromMinutes(15);
    private static readonly IReadOnlyList<string> ChatAllowedStatuses = ["Published"];
    private static readonly IReadOnlyList<string> ManagementAllowedStatuses = ["Draft", "In Review", "Published"];

    private readonly IViewerAccessRepository _repository;
    private readonly IViewerTokenService _tokenService;
    private readonly TimeProvider _timeProvider;
    private readonly string _docsBaseUrl;

    public ViewerAccessService(
        IViewerAccessRepository repository,
        IViewerTokenService tokenService,
        string docsBaseUrl = "https://docs.client.com",
        TimeProvider? timeProvider = null)
    {
        _repository = repository;
        _tokenService = tokenService;
        _docsBaseUrl = docsBaseUrl.TrimEnd('/');
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ViewerLinkResult> CreateLinkAsync(CreateViewerLinkCommand command, CancellationToken ct)
    {
        string purpose = NormalizePurpose(command.Purpose);
        ViewerInstructionAccess instruction = await RequireInstructionAsync(command.InstructionId, ct);
        IReadOnlyList<string> allowedStatuses = AllowedStatusesFor(purpose, command.Roles);
        RequireInstructionAllowed(instruction, allowedStatuses);

        string code = CreateCode();
        DateTimeOffset now = _timeProvider.GetUtcNow();
        DateTimeOffset expiresAt = now.Add(ExchangeCodeTtl);
        await _repository.SaveExchangeCodeAsync(
            new ViewerExchangeCodeRecord(
                Guid.NewGuid(),
                HashCode(code),
                command.InstructionId,
                command.UserId,
                purpose,
                string.Join(',', allowedStatuses),
                expiresAt,
                null,
                now),
            ct);

        return new ViewerLinkResult($"{_docsBaseUrl}/open?code={Uri.EscapeDataString(code)}", expiresAt);
    }

    public async Task<ViewerExchangeResult> ExchangeCodeAsync(ExchangeViewerCodeCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Code))
        {
            throw new ViewerAccessException("VIEWER_CODE_INVALID", 400, "Viewer exchange code is invalid.");
        }

        ViewerExchangeCodeRecord code = await _repository.FindExchangeCodeByHashAsync(HashCode(command.Code), ct)
            ?? throw new ViewerAccessException("VIEWER_CODE_INVALID", 400, "Viewer exchange code is invalid.");
        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (code.ConsumedAt is not null)
        {
            throw new ViewerAccessException("VIEWER_CODE_USED", 410, "Viewer exchange code has already been used.");
        }

        if (code.ExpiresAt <= now)
        {
            throw new ViewerAccessException("VIEWER_CODE_EXPIRED", 410, "Viewer exchange code has expired.");
        }

        ViewerInstructionAccess instruction = await RequireInstructionAsync(code.InstructionId, ct);
        IReadOnlyList<string> allowedStatuses = ParseAllowedStatuses(code.AllowedStatuses);
        RequireInstructionAllowed(instruction, allowedStatuses);

        await _repository.MarkExchangeCodeConsumedAsync(code.Id, now, ct);
        IssuedViewerToken issued = _tokenService.Issue(
            new ViewerTokenIssueRequest(
                code.InstructionId,
                code.UserId,
                code.Purpose,
                allowedStatuses,
                now.Add(ViewerTokenTtl)));
        await _repository.SaveTokenAuditAsync(
            new ViewerTokenAuditRecord(
                Guid.NewGuid(),
                issued.ViewerTokenId,
                issued.InstructionId,
                issued.UserId,
                issued.Purpose,
                now,
                issued.ExpiresAt),
            ct);

        return new ViewerExchangeResult(
            issued.Token,
            issued.ViewerTokenId,
            issued.InstructionId,
            issued.UserId,
            issued.Purpose,
            issued.ExpiresAt);
    }

    public async Task<ViewerDocumentResult> GetDocumentAsync(GetViewerDocumentCommand command, CancellationToken ct)
    {
        ViewerTokenClaims claims = _tokenService.Validate(command.Token);
        ViewerInstructionAccess instruction = await RequireInstructionAsync(claims.InstructionId, ct);
        RequireInstructionAllowed(instruction, claims.AllowedStatuses);
        ViewerInstructionVersion version = SelectVersion(instruction, claims.AllowedStatuses);

        return new ViewerDocumentResult(
            instruction.InstructionId,
            version.Id,
            version.Title,
            instruction.State,
            version.InstructionType,
            version.Audience,
            version.ContentHtml,
            claims.ExpiresAt);
    }

    private async Task<ViewerInstructionAccess> RequireInstructionAsync(Guid instructionId, CancellationToken ct)
    {
        return await _repository.FindInstructionAsync(instructionId, ct)
            ?? throw new ViewerAccessException("NOT_FOUND", 404, "Instruction not found.");
    }

    private static string NormalizePurpose(string purpose)
    {
        string normalized = purpose.Trim().ToLowerInvariant();
        if (normalized is "chat" or "management")
        {
            return normalized;
        }

        throw new ViewerAccessException(
            "VALIDATION_FAILED",
            400,
            "Viewer link purpose is invalid.",
            new Dictionary<string, object?> { ["field"] = "purpose" });
    }

    private static IReadOnlyList<string> AllowedStatusesFor(string purpose, IReadOnlyList<string> roles)
    {
        if (purpose == "chat")
        {
            return ChatAllowedStatuses;
        }

        if (roles.Contains("Admin", StringComparer.Ordinal)
            || roles.Contains("DocumentManager", StringComparer.Ordinal))
        {
            return ManagementAllowedStatuses;
        }

        throw new ViewerAccessException("AUTH_FORBIDDEN", 403, "Viewer link is not allowed.");
    }

    private static void RequireInstructionAllowed(
        ViewerInstructionAccess instruction,
        IReadOnlyList<string> allowedStatuses)
    {
        if (!allowedStatuses.Contains(instruction.State, StringComparer.Ordinal))
        {
            throw new ViewerAccessException("AUTH_FORBIDDEN", 403, "Viewer access is not allowed.");
        }

        _ = SelectVersion(instruction, allowedStatuses);
    }

    private static ViewerInstructionVersion SelectVersion(
        ViewerInstructionAccess instruction,
        IReadOnlyList<string> allowedStatuses)
    {
        if (instruction.State == "Published"
            && allowedStatuses.Contains("Published", StringComparer.Ordinal)
            && instruction.PublishedVersion is not null)
        {
            return instruction.PublishedVersion;
        }

        if ((instruction.State == "Draft" || instruction.State == "In Review")
            && allowedStatuses.Contains(instruction.State, StringComparer.Ordinal)
            && instruction.DraftVersion is not null)
        {
            return instruction.DraftVersion;
        }

        throw new ViewerAccessException("NOT_FOUND", 404, "Instruction version not found.");
    }

    private static IReadOnlyList<string> ParseAllowedStatuses(string value)
    {
        return value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private static string CreateCode()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string HashCode(string code)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
