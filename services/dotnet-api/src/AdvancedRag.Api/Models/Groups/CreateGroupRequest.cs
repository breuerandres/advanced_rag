namespace AdvancedRag.Api.Models.Groups;

public sealed record CreateGroupRequest(
    string Name,
    Guid? OwnerOrganizationalUnitId,
    string? PublishingPolicy);
