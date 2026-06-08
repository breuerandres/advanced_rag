using AdvancedRag.App.Auth;

namespace AdvancedRag.App.Documents;

public sealed record DocumentAccessGroupPolicy(
    Guid GroupId,
    Guid? OwnerOrganizationalUnitId,
    string PublishingPolicy,
    bool HasExplicitPublishGrant);

public interface IDocumentAccessPolicyDataSource
{
    Task<bool> IsSameBranchAsync(Guid firstOrganizationalUnitId, Guid secondOrganizationalUnitId, CancellationToken ct);

    Task<bool> IsDescendantOrSelfAsync(Guid ancestorOrganizationalUnitId, Guid descendantOrganizationalUnitId, CancellationToken ct);

    Task<IReadOnlyList<DocumentAccessGroupPolicy>> GetGroupPoliciesAsync(
        Guid publisherUserId,
        IReadOnlyList<Guid> groupIds,
        CancellationToken ct);
}

public interface IDocumentAccessPolicy
{
    Task<bool> CanReadPublishedDocumentAsync(
        EffectiveAccessScope scope,
        IReadOnlyList<DocumentAccessRuleRecord> rules,
        CancellationToken ct);

    Task<bool> CanManageDraftAsync(
        EffectiveAccessScope scope,
        IReadOnlyList<DocumentAccessRuleRecord> rules,
        CancellationToken ct);

    Task<bool> CanPublishAsync(
        EffectiveAccessScope scope,
        IReadOnlyList<DocumentAccessRuleRecord> rules,
        CancellationToken ct);

    Task<bool> CanUseRuleForPublishingAsync(
        EffectiveAccessScope scope,
        DocumentAccessRuleRecord rule,
        CancellationToken ct);

    Task<bool> CanCreateInOrganizationalUnitAsync(
        EffectiveAccessScope scope,
        Guid organizationalUnitId,
        CancellationToken ct);
}

public sealed class DocumentAccessPolicy : IDocumentAccessPolicy
{
    public static readonly Guid RootOrganizationalUnitId = Guid.Parse("01000000-0000-0000-0000-000000000001");

    private readonly IDocumentAccessPolicyDataSource _dataSource;

    public DocumentAccessPolicy(IDocumentAccessPolicyDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<bool> CanReadPublishedDocumentAsync(
        EffectiveAccessScope scope,
        IReadOnlyList<DocumentAccessRuleRecord> rules,
        CancellationToken ct)
    {
        if (scope.IsGlobalAdmin)
        {
            return true;
        }

        foreach (DocumentAccessRuleRecord rule in rules)
        {
            if (await RuleMatchesReadScopeAsync(scope, rule, ct))
            {
                return true;
            }
        }

        return false;
    }

    public async Task<bool> CanManageDraftAsync(
        EffectiveAccessScope scope,
        IReadOnlyList<DocumentAccessRuleRecord> rules,
        CancellationToken ct)
    {
        if (scope.IsGlobalAdmin)
        {
            return true;
        }

        if (!HasEditorCapability(scope))
        {
            return false;
        }

        return await AllRulesWithinManagementScopeAsync(scope, rules, ct);
    }

    public async Task<bool> CanPublishAsync(
        EffectiveAccessScope scope,
        IReadOnlyList<DocumentAccessRuleRecord> rules,
        CancellationToken ct)
    {
        if (scope.IsGlobalAdmin)
        {
            return true;
        }

        if (!scope.PrimaryRole.Equals("DocumentPublisher", StringComparison.Ordinal))
        {
            return false;
        }

        if (!await AllRulesWithinManagementScopeAsync(scope, rules, ct))
        {
            return false;
        }

        foreach (DocumentAccessRuleRecord rule in rules)
        {
            if (!await CanUseRuleForPublishingAsync(scope, rule, ct))
            {
                return false;
            }
        }

        return true;
    }

    public async Task<bool> CanUseRuleForPublishingAsync(
        EffectiveAccessScope scope,
        DocumentAccessRuleRecord rule,
        CancellationToken ct)
    {
        if (scope.IsGlobalAdmin)
        {
            return true;
        }

        if (!scope.PrimaryRole.Equals("DocumentPublisher", StringComparison.Ordinal))
        {
            return false;
        }

        if (!await RuleOrgUnitWithinManagementScopeAsync(scope, rule, ct))
        {
            return false;
        }

        Guid[] groupIds = rule.GroupIds.Distinct().Order().ToArray();
        if (groupIds.Length == 0)
        {
            return true;
        }

        IReadOnlyList<DocumentAccessGroupPolicy> policies = await _dataSource.GetGroupPoliciesAsync(
            scope.UserId,
            groupIds,
            ct);
        Dictionary<Guid, DocumentAccessGroupPolicy> policiesByGroup = policies.ToDictionary(item => item.GroupId);

        foreach (Guid groupId in groupIds)
        {
            if (!policiesByGroup.TryGetValue(groupId, out DocumentAccessGroupPolicy? policy))
            {
                return false;
            }

            if (!await CanUseGroupForPublishingAsync(scope, policy, ct))
            {
                return false;
            }
        }

        return true;
    }

    public async Task<bool> CanCreateInOrganizationalUnitAsync(
        EffectiveAccessScope scope,
        Guid organizationalUnitId,
        CancellationToken ct)
    {
        if (scope.IsGlobalAdmin)
        {
            return true;
        }

        return HasEditorCapability(scope)
            && await _dataSource.IsDescendantOrSelfAsync(scope.OrganizationalUnitId, organizationalUnitId, ct);
    }

    private async Task<bool> RuleMatchesReadScopeAsync(
        EffectiveAccessScope scope,
        DocumentAccessRuleRecord rule,
        CancellationToken ct)
    {
        if (!IsValidRule(rule))
        {
            return false;
        }

        bool orgMatches = rule.OrganizationalUnitId is null
            || rule.OrganizationalUnitId == RootOrganizationalUnitId
            || await _dataSource.IsSameBranchAsync(rule.OrganizationalUnitId.Value, scope.OrganizationalUnitId, ct);
        bool groupMatches = rule.GroupIds.Count == 0
            || rule.GroupIds.Any(groupId => scope.GroupIds.Contains(groupId));

        return orgMatches && groupMatches;
    }

    private async Task<bool> AllRulesWithinManagementScopeAsync(
        EffectiveAccessScope scope,
        IReadOnlyList<DocumentAccessRuleRecord> rules,
        CancellationToken ct)
    {
        if (rules.Count == 0 || rules.Any(rule => !IsValidRule(rule)))
        {
            return false;
        }

        foreach (DocumentAccessRuleRecord rule in rules)
        {
            if (!await RuleOrgUnitWithinManagementScopeAsync(scope, rule, ct))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<bool> RuleOrgUnitWithinManagementScopeAsync(
        EffectiveAccessScope scope,
        DocumentAccessRuleRecord rule,
        CancellationToken ct)
    {
        return rule.OrganizationalUnitId is not null
            && await _dataSource.IsDescendantOrSelfAsync(scope.OrganizationalUnitId, rule.OrganizationalUnitId.Value, ct);
    }

    private async Task<bool> CanUseGroupForPublishingAsync(
        EffectiveAccessScope scope,
        DocumentAccessGroupPolicy group,
        CancellationToken ct)
    {
        return group.PublishingPolicy switch
        {
            "OwnerScope" => group.OwnerOrganizationalUnitId is not null
                && await _dataSource.IsDescendantOrSelfAsync(
                    scope.OrganizationalUnitId,
                    group.OwnerOrganizationalUnitId.Value,
                    ct),
            "ExplicitGrantOnly" => group.HasExplicitPublishGrant,
            "AdminOnly" => false,
            _ => false,
        };
    }

    private static bool HasEditorCapability(EffectiveAccessScope scope)
    {
        return scope.PrimaryRole.Equals("DocumentEditor", StringComparison.Ordinal)
            || scope.PrimaryRole.Equals("DocumentPublisher", StringComparison.Ordinal);
    }

    private static bool IsValidRule(DocumentAccessRuleRecord rule)
    {
        return rule.OrganizationalUnitId is not null || rule.GroupIds.Count > 0;
    }
}
