using AdvancedRag.App.Auth;
using AdvancedRag.App.Configuration;
using AdvancedRag.App.Users;
using FluentAssertions;

namespace AdvancedRag.App.Tests;

public sealed class UserAdministrationServiceTests
{
    private static readonly Guid ActorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OperationsGroupId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid FinanceGroupId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid RootUnitId = Guid.Parse("01000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task CreateUserAsync_AdminCreatesUserWithDefaultMonthlyAiBudget()
    {
        var repository = new InMemoryUserAdministrationRepository(
            [new RoleRecord("Viewer")],
            [new GroupRecord(OperationsGroupId, "Operations")]);
        var service = new UserAdministrationService(repository, new StubPasswordHashService(), new StubTenantConfigService());

        var created = await service.CreateUserAsync(
            new CreateUserCommand(
                " Viewer.One@Example.COM ",
                "Viewer One",
                "temporary-password",
                ["Viewer"],
                [OperationsGroupId],
                ActorId,
                RootUnitId),
            CancellationToken.None);

        created.Id.Should().Be(UserId);
        created.Email.Should().Be("viewer.one@example.com");
        created.DisplayName.Should().Be("Viewer One");
        created.Roles.Should().Equal("Viewer");
        created.Groups.Should().ContainSingle(group => group.Id == OperationsGroupId);
        created.MonthlyBudgetUsd.Should().Be(5m);
        created.CurrentSpendUsd.Should().Be(0m);
        created.RemainingBudgetUsd.Should().Be(5m);
        created.IsBudgetDisabled.Should().BeFalse();

        repository.Users.Should().ContainSingle().Which.PasswordHash.Should().Be("hashed:temporary-password");
        repository.Budgets.Should().ContainSingle().Which.MonthlyBudgetUsd.Should().Be(5m);
    }

    [Fact]
    public async Task CreateUserAsync_UsesConfiguredDefaultMonthlyBudget()
    {
        var repository = new InMemoryUserAdministrationRepository(
            [new RoleRecord("Viewer")],
            [new GroupRecord(OperationsGroupId, "Operations")]);
        var service = new UserAdministrationService(
            repository,
            new StubPasswordHashService(),
            new StubTenantConfigService(12.00m));

        var created = await service.CreateUserAsync(
            new CreateUserCommand(
                "viewer.two@example.com",
                "Viewer Two",
                "temporary-password",
                ["Viewer"],
                [OperationsGroupId],
                ActorId,
                RootUnitId),
            CancellationToken.None);

        created.MonthlyBudgetUsd.Should().Be(12.00m);
        repository.Budgets.Should().ContainSingle().Which.MonthlyBudgetUsd.Should().Be(12.00m);
    }

    [Fact]
    public async Task SetUserRolesAsync_AdminAssignsRoles()
    {
        var repository = new InMemoryUserAdministrationRepository(
            [new RoleRecord("Viewer"), new RoleRecord("DocumentManager")],
            []);
        repository.AddExistingUser(UserId, "manager@example.com", "Manager", ["Viewer"], []);
        var service = new UserAdministrationService(repository, new StubPasswordHashService(), new StubTenantConfigService());

        var updated = await service.SetUserRolesAsync(
            new SetUserRolesCommand(UserId, ["DocumentManager"], ActorId),
            CancellationToken.None);

        updated.Roles.Should().Equal("DocumentManager");
    }

    [Fact]
    public async Task SetUserGroupsAsync_AdminAssignsGroupsAndChangesAccessScopeHash()
    {
        var repository = new InMemoryUserAdministrationRepository(
            [new RoleRecord("Viewer")],
            [
                new GroupRecord(OperationsGroupId, "Operations"),
                new GroupRecord(FinanceGroupId, "Finance"),
            ]);
        repository.AddExistingUser(UserId, "viewer@example.com", "Viewer", ["Viewer"], [OperationsGroupId]);
        var service = new UserAdministrationService(repository, new StubPasswordHashService(), new StubTenantConfigService());
        var original = await service.GetUserAsync(UserId, CancellationToken.None);

        var updated = await service.SetUserGroupsAsync(
            new SetUserGroupsCommand(UserId, [FinanceGroupId], ActorId),
            CancellationToken.None);

        updated.Groups.Should().ContainSingle(group => group.Id == FinanceGroupId);
        updated.AccessScopeHash.Should().NotBe(original!.AccessScopeHash);
    }

    [Fact]
    public async Task UpdateGroupAsync_AdminRenamesGroup()
    {
        var repository = new InMemoryUserAdministrationRepository(
            [new RoleRecord("Viewer")],
            [new GroupRecord(OperationsGroupId, "Operations")]);
        var service = new UserAdministrationService(repository, new StubPasswordHashService(), new StubTenantConfigService());

        var updated = await service.UpdateGroupAsync(
            new UpdateGroupCommand(OperationsGroupId, "People Operations", ActorId),
            CancellationToken.None);

        updated.Name.Should().Be("People Operations");
    }

    [Fact]
    public async Task SetUserAiBudgetAsync_RejectsNegativeBudgetValues()
    {
        var repository = new InMemoryUserAdministrationRepository([new RoleRecord("Viewer")], []);
        repository.AddExistingUser(UserId, "viewer@example.com", "Viewer", ["Viewer"], []);
        var service = new UserAdministrationService(repository, new StubPasswordHashService(), new StubTenantConfigService());

        var act = () => service.SetUserAiBudgetAsync(
            new SetUserAiBudgetCommand(UserId, -0.01m, false, ActorId),
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<UserAdministrationException>()
            .Where(error => error.Code == "VALIDATION_FAILED");
    }

    private sealed class StubPasswordHashService : IPasswordHashService
    {
        public string Hash(string password) => $"hashed:{password}";

        public bool Verify(string password, string passwordHash) => passwordHash == Hash(password);
    }

    private sealed class StubTenantConfigService : ITenantConfigService
    {
        private readonly decimal _budget;

        public StubTenantConfigService(decimal budget = 5m) => _budget = budget;

        public Task<TenantConfig> GetAsync(CancellationToken ct) =>
            Task.FromResult(TenantConfigDraft.CreateDefault().ToConfigForTest() with { DefaultMonthlyBudgetUsd = _budget });

        public Task<TenantConfig> UpdateAsync(TenantConfigDraft draft, CancellationToken ct) => GetAsync(ct);
    }

    private sealed class InMemoryUserAdministrationRepository : IUserAdministrationRepository
    {
        private readonly IReadOnlyList<RoleRecord> _roles;
        private readonly IReadOnlyList<GroupRecord> _groups;

        public InMemoryUserAdministrationRepository(
            IReadOnlyList<RoleRecord> roles,
            IReadOnlyList<GroupRecord> groups)
        {
            _roles = roles;
            _groups = groups;
        }

        public List<UserDraft> Users { get; } = [];

        public List<UserBudgetDraft> Budgets { get; } = [];

        public Task<IReadOnlyList<UserManagementUser>> ListUsersAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<UserManagementUser>>(
                Users.Select(user => ToUser(user, user.RoleNames, user.GroupIds, Budgets.SingleOrDefault())).ToArray());
        }

        public Task<IReadOnlyList<GroupRecord>> ListGroupsAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(_groups);
        }

        public Task<IReadOnlyList<OrganizationalUnitRecord>> ListActiveOrganizationalUnitsAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<OrganizationalUnitRecord>>([RootUnit()]);
        }

        public Task<GroupRecord> CreateGroupAsync(
            string name,
            Guid? ownerOrganizationalUnitId,
            string publishingPolicy,
            Guid actorUserId,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var group = new GroupRecord(Guid.NewGuid(), name, null, publishingPolicy);
            return Task.FromResult(group);
        }

        public Task<GroupRecord?> UpdateGroupAsync(
            Guid groupId,
            string name,
            Guid? ownerOrganizationalUnitId,
            string publishingPolicy,
            Guid actorUserId,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var group = _groups.SingleOrDefault(item => item.Id == groupId);
            return Task.FromResult(group is null ? null : group with { Name = name, PublishingPolicy = publishingPolicy });
        }

        public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Users.Any(user => user.Email == normalizedEmail));
        }

        public Task<IReadOnlyList<RoleRecord>> FindRolesAsync(IReadOnlyList<string> roleNames, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<RoleRecord>>(
                _roles.Where(role => roleNames.Contains(role.Name, StringComparer.Ordinal)).ToArray());
        }

        public Task<IReadOnlyList<GroupRecord>> FindGroupsAsync(IReadOnlyList<Guid> groupIds, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<GroupRecord>>(
                _groups.Where(group => groupIds.Contains(group.Id)).ToArray());
        }

        public Task<OrganizationalUnitRecord?> FindOrganizationalUnitAsync(
            Guid organizationalUnitId,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<OrganizationalUnitRecord?>(
                organizationalUnitId == RootUnitId ? RootUnit() : null);
        }

        public Task<UserManagementUser> CreateUserAsync(
            UserDraft user,
            IReadOnlyList<string> roleNames,
            IReadOnlyList<Guid> groupIds,
            UserBudgetDraft budget,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            user = user with { Id = UserId };
            Users.Add(user);
            Budgets.Add(budget);
            return Task.FromResult(ToUser(user, roleNames, groupIds, budget));
        }

        public Task<UserManagementUser?> FindUserAsync(Guid userId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var user = Users.SingleOrDefault(item => item.Id == userId);
            return Task.FromResult(user is null ? null : ToUser(user, user.RoleNames, user.GroupIds, Budgets.SingleOrDefault()));
        }

        public Task<UserManagementUser?> SetUserRolesAsync(
            Guid userId,
            IReadOnlyList<string> roleNames,
            Guid actorUserId,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var index = Users.FindIndex(user => user.Id == userId);
            if (index < 0)
            {
                return Task.FromResult<UserManagementUser?>(null);
            }

            Users[index] = Users[index] with { RoleNames = roleNames };
            return FindUserAsync(userId, ct);
        }

        public Task<UserManagementUser?> SetUserGroupsAsync(
            Guid userId,
            IReadOnlyList<Guid> groupIds,
            Guid actorUserId,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var index = Users.FindIndex(user => user.Id == userId);
            if (index < 0)
            {
                return Task.FromResult<UserManagementUser?>(null);
            }

            Users[index] = Users[index] with { GroupIds = groupIds };
            return FindUserAsync(userId, ct);
        }

        public Task<UserManagementUser?> SetUserActiveStatusAsync(
            Guid userId,
            bool isActive,
            Guid actorUserId,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var index = Users.FindIndex(user => user.Id == userId);
            if (index < 0)
            {
                return Task.FromResult<UserManagementUser?>(null);
            }

            Users[index] = Users[index] with { IsActive = isActive };
            return FindUserAsync(userId, ct);
        }

        public Task<UserManagementUser?> SetUserAiBudgetAsync(
            Guid userId,
            decimal? monthlyBudgetUsd,
            bool isDisabled,
            Guid actorUserId,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var user = Users.SingleOrDefault(item => item.Id == userId);
            if (user is null)
            {
                return Task.FromResult<UserManagementUser?>(null);
            }

            Budgets.RemoveAll(budget => budget.UserId == userId);
            Budgets.Add(new UserBudgetDraft(userId, monthlyBudgetUsd, isDisabled, actorUserId));
            return FindUserAsync(userId, ct);
        }

        public Task<UserManagementUser?> SetUserOrganizationalUnitAsync(
            Guid userId,
            Guid organizationalUnitId,
            Guid actorUserId,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var index = Users.FindIndex(user => user.Id == userId);
            if (index < 0)
            {
                return Task.FromResult<UserManagementUser?>(null);
            }

            Users[index] = Users[index] with { OrganizationalUnitId = organizationalUnitId };
            return FindUserAsync(userId, ct);
        }

        public void AddExistingUser(
            Guid id,
            string email,
            string displayName,
            IReadOnlyList<string> roles,
            IReadOnlyList<Guid> groups)
        {
            Users.Add(new UserDraft(id, email, displayName, "hash", true, roles, groups, RootUnitId));
            Budgets.Add(new UserBudgetDraft(id, 5m, false, ActorId));
        }

        private UserManagementUser ToUser(
            UserDraft user,
            IReadOnlyList<string> roleNames,
            IReadOnlyList<Guid> groupIds,
            UserBudgetDraft? budget)
        {
            var groups = _groups
                .Where(group => groupIds.Contains(group.Id))
                .OrderBy(group => group.Name, StringComparer.Ordinal)
                .ToArray();

            var monthlyBudget = budget?.MonthlyBudgetUsd ?? 5m;
            return new UserManagementUser(
                user.Id,
                user.Email,
                user.DisplayName,
                user.IsActive,
                roleNames.Order(StringComparer.Ordinal).ToArray(),
                groups,
                RootUnit(),
                AccessScopeHash.ComputeV2(
                    PrimaryRole(roleNames),
                    roleNames.Contains("Admin", StringComparer.Ordinal),
                    user.OrganizationalUnitId,
                    groups.Select(group => group.Id),
                    1),
                monthlyBudget,
                0m,
                budget?.IsDisabled ?? false);
        }

        private static OrganizationalUnitRecord RootUnit()
        {
            return new OrganizationalUnitRecord(RootUnitId, "Empresa", null, 0, true);
        }

        private static string PrimaryRole(IReadOnlyList<string> roles)
        {
            if (roles.Contains("Admin", StringComparer.Ordinal))
            {
                return "Admin";
            }

            if (roles.Contains("DocumentManager", StringComparer.Ordinal))
            {
                return "DocumentManager";
            }

            return roles.Contains("Viewer", StringComparer.Ordinal) ? "Viewer" : roles.FirstOrDefault() ?? "Viewer";
        }
    }
}
