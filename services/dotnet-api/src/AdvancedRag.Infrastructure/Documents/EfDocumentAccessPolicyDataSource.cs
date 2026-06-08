using AdvancedRag.App.Documents;
using AdvancedRag.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdvancedRag.Infrastructure.Documents;

public sealed class EfDocumentAccessPolicyDataSource : IDocumentAccessPolicyDataSource
{
    private readonly AppDbContext _db;

    public EfDocumentAccessPolicyDataSource(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> IsSameBranchAsync(
        Guid firstOrganizationalUnitId,
        Guid secondOrganizationalUnitId,
        CancellationToken ct)
    {
        return await _db.OrganizationalUnitClosure
            .AsNoTracking()
            .AnyAsync(
                closure =>
                    (closure.AncestorId == firstOrganizationalUnitId
                        && closure.DescendantId == secondOrganizationalUnitId)
                    || (closure.AncestorId == secondOrganizationalUnitId
                        && closure.DescendantId == firstOrganizationalUnitId),
                ct);
    }

    public async Task<bool> IsDescendantOrSelfAsync(
        Guid ancestorOrganizationalUnitId,
        Guid descendantOrganizationalUnitId,
        CancellationToken ct)
    {
        return await _db.OrganizationalUnitClosure
            .AsNoTracking()
            .AnyAsync(
                closure =>
                    closure.AncestorId == ancestorOrganizationalUnitId
                    && closure.DescendantId == descendantOrganizationalUnitId,
                ct);
    }

    public async Task<IReadOnlyList<DocumentAccessGroupPolicy>> GetGroupPoliciesAsync(
        Guid publisherUserId,
        IReadOnlyList<Guid> groupIds,
        CancellationToken ct)
    {
        Guid[] normalizedGroupIds = groupIds.Distinct().Order().ToArray();
        Group[] groups = await _db.Groups
            .AsNoTracking()
            .Where(group => normalizedGroupIds.Contains(group.Id))
            .ToArrayAsync(ct);
        Guid[] grantedGroupIds = await _db.UserGroupPublishGrants
            .AsNoTracking()
            .Where(grant => grant.UserId == publisherUserId && normalizedGroupIds.Contains(grant.GroupId))
            .Select(grant => grant.GroupId)
            .ToArrayAsync(ct);
        HashSet<Guid> grantedGroupIdSet = grantedGroupIds.ToHashSet();

        return groups
            .Select(group => new DocumentAccessGroupPolicy(
                group.Id,
                group.OwnerOrganizationalUnitId,
                group.PublishingPolicy,
                grantedGroupIdSet.Contains(group.Id)))
            .ToArray();
    }
}
