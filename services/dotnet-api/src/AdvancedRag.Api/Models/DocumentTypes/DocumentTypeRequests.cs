namespace AdvancedRag.Api.Models.DocumentTypes;

public sealed record CreateDocumentTypeRequest(string Name, int? SortOrder);

public sealed record UpdateDocumentTypeRequest(string? Name, bool? IsActive, int? SortOrder);
