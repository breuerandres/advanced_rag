namespace AdvancedRag.Api.Models.Viewer;

public sealed record CreateViewerLinkRequest(Guid DocumentId, string Purpose);

public sealed record ConsumeViewerSessionHandoffRequest(Guid DocumentId, string HandoffCode);
