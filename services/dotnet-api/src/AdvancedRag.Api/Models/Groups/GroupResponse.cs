using AdvancedRag.Api.Models.Users;
using AdvancedRag.App.Users;

namespace AdvancedRag.Api.Models.Groups;

public sealed record GroupResponse(
    Guid Id,
    string Name,
    OrganizationalUnitResponse? OwnerOrganizationalUnit,
    string PublishingPolicy)
{
    public static GroupResponse FromGroup(GroupRecord group)
    {
        return new GroupResponse(
            group.Id,
            group.Name,
            group.OwnerOrganizationalUnit is null
                ? null
                : OrganizationalUnitResponse.FromUnit(group.OwnerOrganizationalUnit),
            group.PublishingPolicy);
    }
}
