using AdvancedRag.App.Auth;
using AdvancedRag.App.Configuration;
using AdvancedRag.App.Setup;
using AdvancedRag.App.Users;
using FluentAssertions;

namespace AdvancedRag.App.Tests;

public sealed class SetupServiceTests
{
    private static readonly Guid CreatedAdminId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    [Fact]
    public async Task GetStatusAsync_WhenNoAdminExists_RequiresSetup()
    {
        var repository = new InMemorySetupRepository(adminExists: false);
        var service = new SetupService(repository, new StubPasswordHashService());

        SetupStatus status = await service.GetStatusAsync(CancellationToken.None);

        status.SetupRequired.Should().BeTrue();
        status.AdminExists.Should().BeFalse();
        status.RequiredRoles.Should().Equal("Admin", "DocumentManager", "Viewer");
    }

    [Fact]
    public async Task CreateFirstAdminAsync_WhenSetupIsOpen_CreatesActiveAdminWithDefaultBudget()
    {
        var repository = new InMemorySetupRepository(adminExists: false);
        var service = new SetupService(repository, new StubPasswordHashService());

        FirstAdminResult result = await service.CreateFirstAdminAsync(
            new CreateFirstAdminCommand(
                " Admin.One@Example.COM ",
                " Admin One ",
                "temporary-password"),
            CancellationToken.None);

        result.User.Id.Should().Be(CreatedAdminId);
        result.User.Email.Should().Be("admin.one@example.com");
        result.User.DisplayName.Should().Be("Admin One");
        result.User.Roles.Should().Equal("Admin");
        result.User.MonthlyBudgetUsd.Should().Be(UserAdministrationService.DefaultMonthlyBudgetUsd);
        result.User.IsBudgetDisabled.Should().BeFalse();

        repository.CreatedUsers.Should().ContainSingle().Which.PasswordHash.Should().Be("hashed:temporary-password");
        repository.RequiredRoles.Should().Equal("Admin", "DocumentManager", "Viewer");
        repository.CreatedTenantConfig.Should().NotBeNull();
        repository.CreatedTenantConfig!.BrandName.Should().Be("Help Center");
        repository.CreatedTenantConfig.DefaultLocale.Should().Be("es-AR");
        repository.CreatedTenantConfig.SupportedLocales.Should().Equal("es-AR");
        repository.CreatedTenantConfig.LlmProvider.Should().Be("openai");
        repository.CreatedTenantConfig.EmbeddingDimensions.Should().Be(1024);
    }

    [Fact]
    public async Task CreateFirstAdminAsync_WhenAdminAlreadyExists_BlocksBootstrap()
    {
        var repository = new InMemorySetupRepository(adminExists: true);
        var service = new SetupService(repository, new StubPasswordHashService());

        Func<Task> act = () => service.CreateFirstAdminAsync(
            new CreateFirstAdminCommand("admin@example.com", "Admin", "temporary-password"),
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<SetupException>()
            .Where(error => error.Code == "SETUP_ALREADY_COMPLETED" && error.HttpStatus == 409);
    }

    [Fact]
    public async Task CreateFirstAdminAsync_WhenAdminAlreadyExists_BlocksBeforeValidation()
    {
        var repository = new InMemorySetupRepository(adminExists: true);
        var service = new SetupService(repository, new StubPasswordHashService());

        Func<Task> act = () => service.CreateFirstAdminAsync(
            new CreateFirstAdminCommand(" ", " ", " "),
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<SetupException>()
            .Where(error => error.Code == "SETUP_ALREADY_COMPLETED" && error.HttpStatus == 409);
    }

    [Fact]
    public async Task CreateFirstAdminAsync_WithMissingEmail_ReturnsValidationError()
    {
        var repository = new InMemorySetupRepository(adminExists: false);
        var service = new SetupService(repository, new StubPasswordHashService());

        Func<Task> act = () => service.CreateFirstAdminAsync(
            new CreateFirstAdminCommand(" ", "Admin", "temporary-password"),
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<SetupException>()
            .Where(error => error.Code == "VALIDATION_FAILED");
    }

    private sealed class StubPasswordHashService : IPasswordHashService
    {
        public string Hash(string password) => $"hashed:{password}";

        public bool Verify(string password, string passwordHash) => passwordHash == Hash(password);
    }

    private sealed class InMemorySetupRepository : ISetupRepository
    {
        private bool _adminExists;

        public InMemorySetupRepository(bool adminExists)
        {
            _adminExists = adminExists;
        }

        public List<UserDraft> CreatedUsers { get; } = [];

        public IReadOnlyList<string> RequiredRoles { get; private set; } = [];

        public TenantConfigDraft? CreatedTenantConfig { get; private set; }

        public Task<bool> AdminExistsAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(_adminExists);
        }

        public Task<UserManagementUser?> CreateFirstAdminAsync(
            UserDraft user,
            UserBudgetDraft budget,
            TenantConfigDraft tenantConfig,
            IReadOnlyList<string> requiredRoles,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (_adminExists)
            {
                return Task.FromResult<UserManagementUser?>(null);
            }

            CreatedUsers.Add(user with { Id = CreatedAdminId });
            CreatedTenantConfig = tenantConfig;
            RequiredRoles = requiredRoles;
            _adminExists = true;

            return Task.FromResult<UserManagementUser?>(
                new UserManagementUser(
                    CreatedAdminId,
                    user.Email,
                    user.DisplayName,
                    true,
                    ["Admin"],
                    [],
                    AccessScopeHash.Compute("Admin", []),
                    budget.MonthlyBudgetUsd,
                    0m,
                    budget.IsDisabled));
        }
    }
}
