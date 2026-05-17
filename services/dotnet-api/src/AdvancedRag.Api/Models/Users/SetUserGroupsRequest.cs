namespace AdvancedRag.Api.Models.Users;

public sealed record SetUserGroupsRequest(IReadOnlyList<Guid>? GroupIds);
