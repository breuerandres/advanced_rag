namespace AdvancedRag.Api.Models.Setup;

public sealed record CreateFirstAdminRequest(
    string? Email,
    string? DisplayName,
    string? Password);
