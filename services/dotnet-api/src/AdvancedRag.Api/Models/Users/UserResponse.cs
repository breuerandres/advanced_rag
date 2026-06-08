using AdvancedRag.Api.Models.Groups;
using AdvancedRag.App.Users;

namespace AdvancedRag.Api.Models.Users;

public sealed record UserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    bool IsActive,
    IReadOnlyList<string> Roles,
    IReadOnlyList<GroupResponse> Groups,
    OrganizationalUnitResponse? OrganizationalUnit,
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
            user.OrganizationalUnit is null ? null : OrganizationalUnitResponse.FromUnit(user.OrganizationalUnit),
            user.AccessScopeHash,
            user.MonthlyBudgetUsd,
            user.CurrentSpendUsd,
            user.RemainingBudgetUsd,
            user.IsBudgetDisabled);
    }
}

public sealed record OrganizationalUnitResponse(
    Guid Id,
    string Name,
    Guid? ParentId,
    int Depth,
    bool IsActive)
{
    public static OrganizationalUnitResponse FromUnit(OrganizationalUnitRecord unit)
    {
        return new OrganizationalUnitResponse(unit.Id, unit.Name, unit.ParentId, unit.Depth, unit.IsActive);
    }
}
