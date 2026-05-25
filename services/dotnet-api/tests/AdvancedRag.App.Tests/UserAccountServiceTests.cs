using AdvancedRag.App.Auth;
using FluentAssertions;

namespace AdvancedRag.App.Tests;

public sealed class UserAccountServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task UpdateEmailAsync_NormalizesEmailAndReturnsCurrentSessionUser()
    {
        InMemoryUserAccountRepository repository = new();
        repository.AddUser(UserId, "old@example.com", "Test User", "hashed:current-password");
        UserAccountService service = new(repository, new StubPasswordHashService());

        AuthenticatedUser updated = await service.UpdateEmailAsync(
            new UpdateUserEmailCommand(UserId, " New.Email@Example.COM "),
            CancellationToken.None);

        updated.Email.Should().Be("new.email@example.com");
        repository.Users[UserId].Email.Should().Be("new.email@example.com");
    }

    [Fact]
    public async Task UpdateEmailAsync_RejectsEmailAlreadyUsedByAnotherUser()
    {
        InMemoryUserAccountRepository repository = new();
        repository.AddUser(UserId, "old@example.com", "Test User", "hashed:current-password");
        repository.AddUser(OtherUserId, "used@example.com", "Other User", "hashed:other-password");
        UserAccountService service = new(repository, new StubPasswordHashService());

        Func<Task> act = () => service.UpdateEmailAsync(
            new UpdateUserEmailCommand(UserId, "used@example.com"),
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<UserAccountException>()
            .Where(error => error.Code == "VALIDATION_FAILED");
    }

    [Fact]
    public async Task ChangePasswordAsync_RequiresCurrentPassword()
    {
        InMemoryUserAccountRepository repository = new();
        repository.AddUser(UserId, "old@example.com", "Test User", "hashed:current-password");
        UserAccountService service = new(repository, new StubPasswordHashService());

        Func<Task> act = () => service.ChangePasswordAsync(
            new ChangeUserPasswordCommand(UserId, "wrong-password", "new-password"),
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<UserAccountException>()
            .Where(error => error.Code == "AUTH_REQUIRED");
    }

    [Fact]
    public async Task ChangePasswordAsync_StoresNewPasswordHash()
    {
        InMemoryUserAccountRepository repository = new();
        repository.AddUser(UserId, "old@example.com", "Test User", "hashed:current-password");
        UserAccountService service = new(repository, new StubPasswordHashService());

        await service.ChangePasswordAsync(
            new ChangeUserPasswordCommand(UserId, "current-password", "new-password"),
            CancellationToken.None);

        repository.Users[UserId].PasswordHash.Should().Be("hashed:new-password");
    }

    private sealed class StubPasswordHashService : IPasswordHashService
    {
        public string Hash(string password) => $"hashed:{password}";

        public bool Verify(string password, string passwordHash) => passwordHash == Hash(password);
    }

    private sealed class InMemoryUserAccountRepository : IUserAccountRepository
    {
        public Dictionary<Guid, UserAccountRecord> Users { get; } = [];

        public void AddUser(Guid id, string email, string displayName, string passwordHash)
        {
            Users[id] = new UserAccountRecord(id, email, displayName, passwordHash, ["Viewer"], []);
        }

        public Task<UserAccountRecord?> FindByIdAsync(Guid userId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Users.GetValueOrDefault(userId));
        }

        public Task<bool> EmailExistsForAnotherUserAsync(Guid userId, string normalizedEmail, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Users.Values.Any(user =>
                user.Id != userId &&
                user.Email.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase)));
        }

        public Task<UserAccountRecord> UpdateEmailAsync(Guid userId, string normalizedEmail, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            UserAccountRecord existing = Users[userId];
            UserAccountRecord updated = existing with { Email = normalizedEmail };
            Users[userId] = updated;
            return Task.FromResult(updated);
        }

        public Task UpdatePasswordHashAsync(Guid userId, string passwordHash, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            UserAccountRecord existing = Users[userId];
            Users[userId] = existing with { PasswordHash = passwordHash };
            return Task.CompletedTask;
        }
    }
}
