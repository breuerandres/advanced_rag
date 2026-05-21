using AdvancedRag.Api.Models.Groups;
using AdvancedRag.App.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdvancedRag.Api.Controllers;

[ApiController]
[Route("api/groups")]
public sealed class GroupsController : ApiControllerBase
{
    private readonly IUserAdministrationService _users;

    public GroupsController(IUserAdministrationService users)
    {
        _users = users;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,DocumentManager")]
    public async Task<IActionResult> ListGroupsAsync(CancellationToken ct)
    {
        IReadOnlyList<GroupRecord> groups = await _users.ListGroupsAsync(ct);
        return Ok(groups.Select(GroupResponse.FromGroup).ToArray());
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateGroupAsync([FromBody] CreateGroupRequest request, CancellationToken ct)
    {
        try
        {
            GroupRecord group = await _users.CreateGroupAsync(
                new CreateGroupCommand(request.Name, ActorUserId()),
                ct);
            return Created($"/api/groups/{group.Id}", GroupResponse.FromGroup(group));
        }
        catch (UserAdministrationException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateGroupAsync(
        Guid id,
        [FromBody] UpdateGroupRequest request,
        CancellationToken ct)
    {
        try
        {
            GroupRecord group = await _users.UpdateGroupAsync(
                new UpdateGroupCommand(id, request.Name, ActorUserId()),
                ct);
            return Ok(GroupResponse.FromGroup(group));
        }
        catch (UserAdministrationException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }
}
