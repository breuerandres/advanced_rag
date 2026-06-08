namespace AdvancedRag.Api.Models.OrganizationalUnits;

public sealed record CreateOrganizationalUnitRequest(string Name, Guid? ParentId);

public sealed record UpdateOrganizationalUnitRequest(string? Name, bool? IsActive, Guid? ParentId);
