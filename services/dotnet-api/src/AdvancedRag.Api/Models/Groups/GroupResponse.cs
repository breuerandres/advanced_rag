using AdvancedRag.App.Users;

namespace AdvancedRag.Api.Models.Groups;

public sealed record GroupResponse(Guid Id, string Name)
{
    public static GroupResponse FromGroup(GroupRecord group)
    {
        return new GroupResponse(group.Id, group.Name);
    }
}
