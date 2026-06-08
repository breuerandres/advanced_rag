namespace AdvancedRag.Api.Models.Groups;

public sealed record UpdateGroupRequest(
    string Name,
    Guid? OwnerOrganizationalUnitId,
    string? PublishingPolicy);
