using AdvancedRag.Api.Models.OrganizationalUnits;
using AdvancedRag.Api.Models.Users;
using AdvancedRag.App.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdvancedRag.Api.Controllers;

[ApiController]
[Route("api/organizational-units")]
[Authorize(Roles = "Admin,DocumentEditor,DocumentPublisher")]
public sealed class OrganizationalUnitsController : ApiControllerBase
{
    private readonly IOrganizationalUnitService _organizationalUnits;

    public OrganizationalUnitsController(IOrganizationalUnitService organizationalUnits)
    {
        _organizationalUnits = organizationalUnits;
    }

    [HttpGet]
    public async Task<IActionResult> ListAsync([FromQuery] bool includeInactive, CancellationToken ct)
    {
        IReadOnlyList<OrganizationalUnitRecord> units = await _organizationalUnits.ListTreeAsync(includeInactive, ct);
        return Ok(units.Select(OrganizationalUnitResponse.FromUnit).ToArray());
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateOrganizationalUnitRequest request,
        CancellationToken ct)
    {
        if (request.ParentId is null)
        {
            return Error(
                400,
                "VALIDATION_FAILED",
                "Parent organizational unit is required.",
                new Dictionary<string, object?> { ["field"] = "parentId" });
        }

        try
        {
            OrganizationalUnitRecord unit = await _organizationalUnits.CreateAsync(
                new CreateOrganizationalUnitCommand(request.Name, request.ParentId.Value, ActorUserId()),
                ct);
            return Created($"/api/organizational-units/{unit.Id}", OrganizationalUnitResponse.FromUnit(unit));
        }
        catch (UserAdministrationException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateAsync(
        Guid id,
        [FromBody] UpdateOrganizationalUnitRequest request,
        CancellationToken ct)
    {
        if (request.ParentId is not null)
        {
            return Error(
                400,
                "VALIDATION_FAILED",
                "Moving organizational-unit branches is not exposed in this slice.",
                new Dictionary<string, object?> { ["field"] = "parentId" });
        }

        try
        {
            OrganizationalUnitRecord unit = await _organizationalUnits.UpdateAsync(
                new UpdateOrganizationalUnitCommand(id, request.Name, request.IsActive, ActorUserId()),
                ct);
            return Ok(OrganizationalUnitResponse.FromUnit(unit));
        }
        catch (UserAdministrationException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }
}
