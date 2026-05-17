using AdvancedRag.Api.Models.Users;
using AdvancedRag.App.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdvancedRag.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public sealed class UsersController : ApiControllerBase
{
    private readonly IUserAdministrationService _users;

    public UsersController(IUserAdministrationService users)
    {
        _users = users;
    }

    [HttpGet]
    public async Task<IActionResult> ListUsersAsync(CancellationToken ct)
    {
        var result = await _users.ListUsersAsync(ct);
        return Ok(result.Select(UserResponse.FromUser).ToArray());
    }

    [HttpPost]
    public async Task<IActionResult> CreateUserAsync([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        try
        {
            var created = await _users.CreateUserAsync(
                new CreateUserCommand(
                    request.Email,
                    request.DisplayName,
                    request.Password,
                    request.Roles ?? [],
                    request.GroupIds ?? [],
                    ActorUserId()),
                ct);
            return Created($"/api/users/{created.Id}", UserResponse.FromUser(created));
        }
        catch (UserAdministrationException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }

    [HttpPut("{id:guid}/roles")]
    public async Task<IActionResult> SetUserRolesAsync(
        Guid id,
        [FromBody] SetUserRolesRequest request,
        CancellationToken ct)
    {
        try
        {
            var updated = await _users.SetUserRolesAsync(
                new SetUserRolesCommand(id, request.Roles ?? [], ActorUserId()),
                ct);
            return Ok(UserResponse.FromUser(updated));
        }
        catch (UserAdministrationException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }

    [HttpPut("{id:guid}/groups")]
    public async Task<IActionResult> SetUserGroupsAsync(
        Guid id,
        [FromBody] SetUserGroupsRequest request,
        CancellationToken ct)
    {
        try
        {
            var updated = await _users.SetUserGroupsAsync(
                new SetUserGroupsCommand(id, request.GroupIds ?? [], ActorUserId()),
                ct);
            return Ok(UserResponse.FromUser(updated));
        }
        catch (UserAdministrationException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> SetUserStatusAsync(
        Guid id,
        [FromBody] SetUserStatusRequest request,
        CancellationToken ct)
    {
        try
        {
            var updated = await _users.SetUserActiveStatusAsync(
                new SetUserActiveStatusCommand(id, request.IsActive, ActorUserId()),
                ct);
            return Ok(UserResponse.FromUser(updated));
        }
        catch (UserAdministrationException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }

    [HttpPut("{id:guid}/ai-budget")]
    public async Task<IActionResult> SetAiBudgetAsync(
        Guid id,
        [FromBody] SetAiBudgetRequest request,
        CancellationToken ct)
    {
        try
        {
            var updated = await _users.SetUserAiBudgetAsync(
                new SetUserAiBudgetCommand(
                    id,
                    request.MonthlyBudgetUsd,
                    request.IsDisabled,
                    ActorUserId()),
                ct);
            return Ok(UserResponse.FromUser(updated));
        }
        catch (UserAdministrationException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }
}
