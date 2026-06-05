using System.Security.Cryptography;
using System.Text;

namespace AdvancedRag.App.Auth;

public sealed class SessionHandoffService : ISessionHandoffService
{
    private static readonly TimeSpan HandoffTtl = TimeSpan.FromSeconds(60);
    private static readonly IReadOnlySet<string> AllowedTargets = new HashSet<string>(StringComparer.Ordinal)
    {
        "chat",
        "docs",
    };

    private readonly ISessionHandoffRepository _repository;
    private readonly TimeProvider _timeProvider;

    public SessionHandoffService(ISessionHandoffRepository repository, TimeProvider? timeProvider = null)
    {
        _repository = repository;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<SessionHandoffResult> CreateAsync(CreateSessionHandoffCommand command, CancellationToken ct)
    {
        string target = NormalizeTarget(command.Target);
        DateTimeOffset now = _timeProvider.GetUtcNow();
        DateTimeOffset expiresAt = now.Add(HandoffTtl);
        string handoffCode = GenerateHandoffCode();
        SessionHandoffRecord record = new(
            Guid.NewGuid(),
            HashHandoffCode(handoffCode),
            command.UserId,
            target,
            expiresAt,
            null,
            now,
            string.IsNullOrWhiteSpace(command.RequestId) ? Guid.NewGuid().ToString("N") : command.RequestId);

        await _repository.StoreAsync(record, ct);
        return new SessionHandoffResult(target, handoffCode, expiresAt);
    }

    public async Task<SessionHandoffConsumeResult> ConsumeAsync(ConsumeSessionHandoffCommand command, CancellationToken ct)
    {
        string target = NormalizeTarget(command.Target);
        if (string.IsNullOrWhiteSpace(command.HandoffCode))
        {
            throw new SessionHandoffException("SESSION_HANDOFF_INVALID", 401, "Session handoff code is invalid.");
        }

        string codeHash = HashHandoffCode(command.HandoffCode);
        SessionHandoffRecord record = await _repository.FindByCodeHashAsync(codeHash, ct)
            ?? throw new SessionHandoffException("SESSION_HANDOFF_INVALID", 401, "Session handoff code is invalid.");

        if (!string.Equals(record.Target, target, StringComparison.Ordinal))
        {
            throw new SessionHandoffException("SESSION_HANDOFF_INVALID", 401, "Session handoff code is invalid.");
        }

        if (record.ConsumedAt is not null)
        {
            throw new SessionHandoffException("SESSION_HANDOFF_USED", 410, "Session handoff code has already been used.");
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (record.ExpiresAt <= now)
        {
            throw new SessionHandoffException("SESSION_HANDOFF_EXPIRED", 410, "Session handoff code has expired.");
        }

        await _repository.MarkConsumedAsync(record.Id, now, ct);
        return new SessionHandoffConsumeResult(record.UserId, record.Target, record.ExpiresAt);
    }

    private static string NormalizeTarget(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            throw InvalidTarget();
        }

        string normalized = target.Trim().ToLowerInvariant();
        if (AllowedTargets.Contains(normalized))
        {
            return normalized;
        }

        throw InvalidTarget();
    }

    private static SessionHandoffException InvalidTarget()
    {
        throw new SessionHandoffException(
            "VALIDATION_FAILED",
            400,
            "Session handoff target is invalid.",
            new Dictionary<string, object?> { ["field"] = "target" });
    }

    private static string GenerateHandoffCode()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string HashHandoffCode(string code)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code))).ToLowerInvariant();
    }
}
