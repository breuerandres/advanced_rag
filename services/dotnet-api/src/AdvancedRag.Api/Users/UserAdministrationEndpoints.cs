using System.Security.Claims;
using AdvancedRag.Api.Errors;
using AdvancedRag.Api.Middleware;
using AdvancedRag.App.Users;

namespace AdvancedRag.Api.Users;

public static class UserAdministrationEndpoints
{
    public static IEndpointRouteBuilder MapUserAdministrationEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/api/users")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        users.MapGet("", ListUsersAsync);
        users.MapPost("", CreateUserAsync);
        users.MapPut("{id:guid}/roles", SetUserRolesAsync);
        users.MapPut("{id:guid}/groups", SetUserGroupsAsync);
        users.MapPatch("{id:guid}/status", SetUserStatusAsync);
        users.MapPut("{id:guid}/ai-budget", SetAiBudgetAsync);

        app.MapGet("/api/groups", ListGroupsAsync)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "DocumentManager"));
        app.MapPost("/api/groups", CreateGroupAsync)
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        return app;
    }

    private static async Task<IResult> ListUsersAsync(
        IUserAdministrationService users,
        CancellationToken ct)
    {
        var result = await users.ListUsersAsync(ct);
        return Results.Ok(result.Select(UserResponse.FromUser).ToArray());
    }

    private static async Task<IResult> ListGroupsAsync(
        IUserAdministrationService users,
        CancellationToken ct)
    {
        var result = await users.ListGroupsAsync(ct);
        return Results.Ok(result.Select(GroupResponse.FromGroup).ToArray());
    }

    private static async Task<IResult> CreateGroupAsync(
        CreateGroupRequest request,
        HttpContext context,
        IUserAdministrationService users,
        CancellationToken ct)
    {
        try
        {
            var group = await users.CreateGroupAsync(
                new CreateGroupCommand(request.Name, ActorUserId(context)),
                ct);
            return Results.Created($"/api/groups/{group.Id}", GroupResponse.FromGroup(group));
        }
        catch (UserAdministrationException exception)
        {
            return Error(context, exception);
        }
    }

    private static async Task<IResult> CreateUserAsync(
        CreateUserRequest request,
        HttpContext context,
        IUserAdministrationService users,
        CancellationToken ct)
    {
        try
        {
            var created = await users.CreateUserAsync(
                new CreateUserCommand(
                    request.Email,
                    request.DisplayName,
                    request.Password,
                    request.Roles ?? [],
                    request.GroupIds ?? [],
                    ActorUserId(context)),
                ct);
            return Results.Created($"/api/users/{created.Id}", UserResponse.FromUser(created));
        }
        catch (UserAdministrationException exception)
        {
            return Error(context, exception);
        }
    }

    private static async Task<IResult> SetUserRolesAsync(
        Guid id,
        SetUserRolesRequest request,
        HttpContext context,
        IUserAdministrationService users,
        CancellationToken ct)
    {
        try
        {
            var updated = await users.SetUserRolesAsync(
                new SetUserRolesCommand(id, request.Roles ?? [], ActorUserId(context)),
                ct);
            return Results.Ok(UserResponse.FromUser(updated));
        }
        catch (UserAdministrationException exception)
        {
            return Error(context, exception);
        }
    }

    private static async Task<IResult> SetUserGroupsAsync(
        Guid id,
        SetUserGroupsRequest request,
        HttpContext context,
        IUserAdministrationService users,
        CancellationToken ct)
    {
        try
        {
            var updated = await users.SetUserGroupsAsync(
                new SetUserGroupsCommand(id, request.GroupIds ?? [], ActorUserId(context)),
                ct);
            return Results.Ok(UserResponse.FromUser(updated));
        }
        catch (UserAdministrationException exception)
        {
            return Error(context, exception);
        }
    }

    private static async Task<IResult> SetUserStatusAsync(
        Guid id,
        SetUserStatusRequest request,
        HttpContext context,
        IUserAdministrationService users,
        CancellationToken ct)
    {
        try
        {
            var updated = await users.SetUserActiveStatusAsync(
                new SetUserActiveStatusCommand(id, request.IsActive, ActorUserId(context)),
                ct);
            return Results.Ok(UserResponse.FromUser(updated));
        }
        catch (UserAdministrationException exception)
        {
            return Error(context, exception);
        }
    }

    private static async Task<IResult> SetAiBudgetAsync(
        Guid id,
        SetAiBudgetRequest request,
        HttpContext context,
        IUserAdministrationService users,
        CancellationToken ct)
    {
        try
        {
            var updated = await users.SetUserAiBudgetAsync(
                new SetUserAiBudgetCommand(
                    id,
                    request.MonthlyBudgetUsd,
                    request.IsDisabled,
                    ActorUserId(context)),
                ct);
            return Results.Ok(UserResponse.FromUser(updated));
        }
        catch (UserAdministrationException exception)
        {
            return Error(context, exception);
        }
    }

    private static Guid ActorUserId(HttpContext context)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userId, out var parsed) ? parsed : Guid.Empty;
    }

    private static IResult Error(HttpContext context, UserAdministrationException exception)
    {
        var requestId = context.Items.TryGetValue(RequestIdMiddleware.ContextItemKey, out var value)
            ? value?.ToString() ?? string.Empty
            : string.Empty;

        return Results.Json(
            ErrorResponse.Create(exception.Code, exception.Message, requestId, exception.Details),
            statusCode: exception.HttpStatus);
    }
}

public sealed record CreateUserRequest(
    string Email,
    string DisplayName,
    string Password,
    IReadOnlyList<string>? Roles,
    IReadOnlyList<Guid>? GroupIds);

public sealed record SetUserRolesRequest(IReadOnlyList<string>? Roles);

public sealed record SetUserGroupsRequest(IReadOnlyList<Guid>? GroupIds);

public sealed record SetUserStatusRequest(bool IsActive);

public sealed record SetAiBudgetRequest(decimal? MonthlyBudgetUsd, bool IsDisabled);

public sealed record CreateGroupRequest(string Name);

public sealed record UserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    bool IsActive,
    IReadOnlyList<string> Roles,
    IReadOnlyList<GroupResponse> Groups,
    string AccessScopeHash,
    decimal? MonthlyBudgetUsd,
    decimal CurrentSpendUsd,
    decimal? RemainingBudgetUsd,
    bool IsBudgetDisabled)
{
    public static UserResponse FromUser(UserManagementUser user)
    {
        return new UserResponse(
            user.Id,
            user.Email,
            user.DisplayName,
            user.IsActive,
            user.Roles,
            user.Groups.Select(GroupResponse.FromGroup).ToArray(),
            user.AccessScopeHash,
            user.MonthlyBudgetUsd,
            user.CurrentSpendUsd,
            user.RemainingBudgetUsd,
            user.IsBudgetDisabled);
    }
}

public sealed record GroupResponse(Guid Id, string Name)
{
    public static GroupResponse FromGroup(GroupRecord group)
    {
        return new GroupResponse(group.Id, group.Name);
    }
}
