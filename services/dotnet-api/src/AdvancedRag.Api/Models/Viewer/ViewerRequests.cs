namespace AdvancedRag.Api.Models.Viewer;

public sealed record CreateViewerLinkRequest(Guid InstructionId, string Purpose);

public sealed record ExchangeViewerCodeRequest(string Code);
