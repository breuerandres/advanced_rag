namespace AdvancedRag.Api.Models.Viewer;

public sealed record CreateViewerLinkRequest(Guid DocumentId, string Purpose);
