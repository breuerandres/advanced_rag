namespace AdvancedRag.Api.Models.Documents;

public sealed record DocumentImageUploadResponse(
    Guid ImageId,
    string Url,
    string AltText);
