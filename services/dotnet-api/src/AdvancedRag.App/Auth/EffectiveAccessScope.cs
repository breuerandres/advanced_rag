namespace AdvancedRag.App.Auth;

public sealed record EffectiveAccessScope(
    Guid UserId,
    string PrimaryRole,
    bool IsGlobalAdmin,
    Guid OrganizationalUnitId,
    IReadOnlyList<Guid> GroupIds,
    long AccessScopeVersion,
    string Corpus)
{
    public string AccessScopeHash => AdvancedRag.App.Auth.AccessScopeHash.ComputeV2(
        PrimaryRole,
        IsGlobalAdmin,
        OrganizationalUnitId,
        GroupIds,
        AccessScopeVersion);
}

public interface IEffectiveAccessScopeRepository
{
    Task<EffectiveAccessScope?> FindForActiveUserAsync(Guid userId, CancellationToken ct);
}
