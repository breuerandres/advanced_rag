namespace AdvancedRag.App.Auth;

public interface ISessionHandoffService
{
    Task<SessionHandoffResult> CreateAsync(CreateSessionHandoffCommand command, CancellationToken ct);

    Task<SessionHandoffConsumeResult> ConsumeAsync(ConsumeSessionHandoffCommand command, CancellationToken ct);
}

public interface ISessionHandoffRepository
{
    Task StoreAsync(SessionHandoffRecord record, CancellationToken ct);

    Task<SessionHandoffRecord?> FindByCodeHashAsync(string codeHash, CancellationToken ct);

    Task MarkConsumedAsync(Guid id, DateTimeOffset consumedAt, CancellationToken ct);
}

public sealed record CreateSessionHandoffCommand(
    Guid UserId,
    string Target,
    string RequestId);

public sealed record ConsumeSessionHandoffCommand(
    string HandoffCode,
    string Target);

public sealed record SessionHandoffResult(
    string Target,
    string HandoffCode,
    DateTimeOffset ExpiresAt);

public sealed record SessionHandoffConsumeResult(
    Guid UserId,
    string Target,
    DateTimeOffset ExpiresAt);

public sealed record SessionHandoffRecord(
    Guid Id,
    string CodeHash,
    Guid UserId,
    string Target,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? ConsumedAt,
    DateTimeOffset CreatedAt,
    string RequestId);

public sealed class SessionHandoffException : Exception
{
    public SessionHandoffException(
        string code,
        int httpStatus,
        string message,
        IReadOnlyDictionary<string, object?>? details = null)
        : base(message)
    {
        Code = code;
        HttpStatus = httpStatus;
        Details = details ?? new Dictionary<string, object?>();
    }

    public string Code { get; }

    public int HttpStatus { get; }

    public IReadOnlyDictionary<string, object?> Details { get; }
}
