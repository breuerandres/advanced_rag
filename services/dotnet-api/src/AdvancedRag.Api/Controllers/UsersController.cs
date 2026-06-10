using AdvancedRag.Api.Models.Users;
using AdvancedRag.App.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdvancedRag.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController : ApiControllerBase
{
    private readonly IUserAdministrationService _users;

    public UsersController(IUserAdministrationService users)
    {
        _users = users;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,DocumentEditor,DocumentPublisher")]
    public async Task<IActionResult> ListUsersAsync(CancellationToken ct)
    {
        IReadOnlyList<UserManagementUser> users = await _users.ListUsersAsync(ct);
        return Ok(users.Select(UserResponse.FromUser).ToArray());
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateUserAsync([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        try
        {
            UserManagementUser created = await _users.CreateUserAsync(
                new CreateUserCommand(
                    request.Email,
                    request.DisplayName,
                    request.Password,
                    request.Roles ?? [],
                    request.GroupIds ?? [],
                    ActorUserId(),
                    request.OrganizationalUnitId),
                ct);
            return Created($"/api/users/{created.Id}", UserResponse.FromUser(created));
        }
        catch (UserAdministrationException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }

    [HttpPut("{id:guid}/roles")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetUserRolesAsync(
        Guid id,
        [FromBody] SetUserRolesRequest request,
        CancellationToken ct)
    {
        try
        {
            UserManagementUser updated = await _users.SetUserRolesAsync(
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
    [Authorize(Roles = "Admin,DocumentEditor,DocumentPublisher")]
    public async Task<IActionResult> SetUserGroupsAsync(
        Guid id,
        [FromBody] SetUserGroupsRequest request,
        CancellationToken ct)
    {
        if (!ActorRoles().Contains("Admin", StringComparer.Ordinal) && id == ActorUserId())
        {
            return Error(403, "AUTH_FORBIDDEN", "Non-admin users cannot broaden their own group scope.");
        }

        try
        {
            UserManagementUser updated = await _users.SetUserGroupsAsync(
                new SetUserGroupsCommand(id, request.GroupIds ?? [], ActorUserId()),
                ct);
            return Ok(UserResponse.FromUser(updated));
        }
        catch (UserAdministrationException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }

    [HttpPut("{id:guid}/organizational-unit")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetUserOrganizationalUnitAsync(
        Guid id,
        [FromBody] SetUserOrganizationalUnitRequest request,
        CancellationToken ct)
    {
        try
        {
            UserManagementUser updated = await _users.SetUserOrganizationalUnitAsync(
                new SetUserOrganizationalUnitCommand(id, request.OrganizationalUnitId, ActorUserId()),
                ct);
            return Ok(UserResponse.FromUser(updated));
        }
        catch (UserAdministrationException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }

    private IReadOnlyList<string> ActorRoles()
    {
        return User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(claim => claim.Value).ToArray();
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetUserStatusAsync(
        Guid id,
        [FromBody] SetUserStatusRequest request,
        CancellationToken ct)
    {
        try
        {
            UserManagementUser updated = await _users.SetUserActiveStatusAsync(
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
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetAiBudgetAsync(
        Guid id,
        [FromBody] SetAiBudgetRequest request,
        CancellationToken ct)
    {
        try
        {
            UserManagementUser updated = await _users.SetUserAiBudgetAsync(
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
