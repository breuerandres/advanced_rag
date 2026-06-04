using System.Security.Cryptography;
using System.Text;

namespace AdvancedRag.App.Viewer;

public sealed class ViewerAccessService : IViewerAccessService
{
    private static readonly IReadOnlyList<string> ChatAllowedStatuses = ["Published"];
    private static readonly IReadOnlyList<string> ManagementAllowedStatuses = ["Draft", "In Review", "Published"];
    private static readonly TimeSpan HandoffTtl = TimeSpan.FromSeconds(60);

    private readonly IViewerAccessRepository _repository;
    private readonly IViewerSessionHandoffRepository _handoffs;
    private readonly string _docsBaseUrl;
    private readonly TimeProvider _timeProvider;

    public ViewerAccessService(
        IViewerAccessRepository repository,
        IViewerSessionHandoffRepository handoffs,
        string docsBaseUrl = "https://docs.client.com",
        TimeProvider? timeProvider = null)
    {
        _repository = repository;
        _handoffs = handoffs;
        _docsBaseUrl = docsBaseUrl.TrimEnd('/');
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ViewerLinkResult> CreateLinkAsync(CreateViewerLinkCommand command, CancellationToken ct)
    {
        string purpose = NormalizePurpose(command.Purpose);
        ViewerDocumentAccess document = await RequireDocumentAsync(command.DocumentId, ct);
        IReadOnlyList<string> allowedStatuses = AllowedStatusesFor(purpose, command.Roles);
        RequireDocumentAllowed(document, allowedStatuses);

        DateTimeOffset now = _timeProvider.GetUtcNow();
        DateTimeOffset expiresAt = now.Add(HandoffTtl);
        string handoffCode = GenerateHandoffCode();
        ViewerSessionHandoffRecord record = new(
            Guid.NewGuid(),
            HashHandoffCode(handoffCode),
            command.UserId,
            command.DocumentId,
            purpose,
            string.Join(',', allowedStatuses),
            expiresAt,
            null,
            now,
            string.IsNullOrWhiteSpace(command.RequestId) ? Guid.NewGuid().ToString("N") : command.RequestId);
        await _handoffs.StoreAsync(record, ct);

        string url = $"{_docsBaseUrl}/open?documentId={Uri.EscapeDataString(command.DocumentId.ToString())}&handoff={Uri.EscapeDataString(handoffCode)}";
        return new ViewerLinkResult(url, expiresAt);
    }

    public async Task<ViewerSessionHandoffResult> ConsumeHandoffAsync(
        ConsumeViewerSessionHandoffCommand command,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.HandoffCode))
        {
            throw new ViewerAccessException("VIEWER_HANDOFF_INVALID", 401, "Viewer handoff code is invalid.");
        }

        string codeHash = HashHandoffCode(command.HandoffCode);
        ViewerSessionHandoffRecord record = await _handoffs.FindByCodeHashAsync(codeHash, ct)
            ?? throw new ViewerAccessException("VIEWER_HANDOFF_INVALID", 401, "Viewer handoff code is invalid.");

        if (record.DocumentId != command.DocumentId)
        {
            throw new ViewerAccessException("VIEWER_HANDOFF_INVALID", 401, "Viewer handoff code is invalid.");
        }

        if (record.ConsumedAt is not null)
        {
            throw new ViewerAccessException("VIEWER_HANDOFF_USED", 410, "Viewer handoff code has already been used.");
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (record.ExpiresAt <= now)
        {
            throw new ViewerAccessException("VIEWER_HANDOFF_EXPIRED", 410, "Viewer handoff code has expired.");
        }

        IReadOnlyList<string> allowedStatuses = ParseAllowedStateScope(record.AllowedStateScope);
        ViewerDocumentAccess document = await RequireDocumentAsync(command.DocumentId, ct);
        RequireDocumentAllowed(document, allowedStatuses);

        await _handoffs.MarkConsumedAsync(record.Id, now, ct);
        return new ViewerSessionHandoffResult(record.UserId, allowedStatuses, record.ExpiresAt);
    }

    public async Task<ViewerDocumentResult> GetDocumentAsync(GetViewerDocumentCommand command, CancellationToken ct)
    {
        ViewerDocumentAccess document = await RequireDocumentAsync(command.DocumentId, ct);
        IReadOnlyList<string> allowedStatuses = AllowedStatusesForSession(command.Roles);
        RequireDocumentAllowed(document, allowedStatuses);
        ViewerDocumentVersion version = SelectVersion(document, allowedStatuses);

        return new ViewerDocumentResult(
            document.DocumentId,
            version.Id,
            version.Title,
            document.State,
            version.DocumentType,
            version.Audience,
            version.ContentHtml,
            DateTimeOffset.MaxValue);
    }

    private async Task<ViewerDocumentAccess> RequireDocumentAsync(Guid documentId, CancellationToken ct)
    {
        return await _repository.FindDocumentAsync(documentId, ct)
            ?? throw new ViewerAccessException("NOT_FOUND", 404, "Document not found.");
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

    private static void RequireDocumentAllowed(
        ViewerDocumentAccess document,
        IReadOnlyList<string> allowedStatuses)
    {
        if (!allowedStatuses.Contains(document.State, StringComparer.Ordinal))
        {
            throw new ViewerAccessException("AUTH_FORBIDDEN", 403, "Viewer access is not allowed.");
        }

        _ = SelectVersion(document, allowedStatuses);
    }

    private static ViewerDocumentVersion SelectVersion(
        ViewerDocumentAccess document,
        IReadOnlyList<string> allowedStatuses)
    {
        if (document.State == "Published"
            && allowedStatuses.Contains("Published", StringComparer.Ordinal)
            && document.PublishedVersion is not null)
        {
            return document.PublishedVersion;
        }

        if ((document.State == "Draft" || document.State == "In Review")
            && allowedStatuses.Contains(document.State, StringComparer.Ordinal)
            && document.DraftVersion is not null)
        {
            return document.DraftVersion;
        }

        throw new ViewerAccessException("NOT_FOUND", 404, "Document version not found.");
    }

    private static IReadOnlyList<string> AllowedStatusesForSession(IReadOnlyList<string> roles)
    {
        return roles.Contains("Admin", StringComparer.Ordinal)
            || roles.Contains("DocumentManager", StringComparer.Ordinal)
                ? ManagementAllowedStatuses
                : ChatAllowedStatuses;
    }

    private static string GenerateHandoffCode()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string HashHandoffCode(string code)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code))).ToLowerInvariant();
    }

    private static IReadOnlyList<string> ParseAllowedStateScope(string allowedStateScope)
    {
        string[] states = allowedStateScope
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return states.Length == 0
            ? throw new ViewerAccessException("VIEWER_HANDOFF_INVALID", 401, "Viewer handoff code is invalid.")
            : states;
    }
}
