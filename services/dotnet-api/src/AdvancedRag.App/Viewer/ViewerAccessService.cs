namespace AdvancedRag.App.Viewer;

public sealed class ViewerAccessService : IViewerAccessService
{
    private static readonly IReadOnlyList<string> ChatAllowedStatuses = ["Published"];
    private static readonly IReadOnlyList<string> ManagementAllowedStatuses = ["Draft", "In Review", "Published"];

    private readonly IViewerAccessRepository _repository;
    private readonly string _docsBaseUrl;

    public ViewerAccessService(
        IViewerAccessRepository repository,
        string docsBaseUrl = "https://docs.client.com",
        TimeProvider? timeProvider = null)
    {
        _repository = repository;
        _docsBaseUrl = docsBaseUrl.TrimEnd('/');
        _ = timeProvider;
    }

    public async Task<ViewerLinkResult> CreateLinkAsync(CreateViewerLinkCommand command, CancellationToken ct)
    {
        string purpose = NormalizePurpose(command.Purpose);
        ViewerDocumentAccess document = await RequireDocumentAsync(command.DocumentId, ct);
        IReadOnlyList<string> allowedStatuses = AllowedStatusesFor(purpose, command.Roles);
        RequireDocumentAllowed(document, allowedStatuses);

        string url = $"{_docsBaseUrl}/open?documentId={Uri.EscapeDataString(command.DocumentId.ToString())}";
        return new ViewerLinkResult(url, DateTimeOffset.MaxValue);
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
}
