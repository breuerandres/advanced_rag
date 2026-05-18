using AdvancedRag.Api.Security;
using AdvancedRag.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdvancedRag.Api.Health;

public interface IOperationalReadinessChecker
{
    Task<OperationalReadinessResult> CheckAsync(CancellationToken ct);
}

public sealed record OperationalReadinessResult(bool IsReady, IReadOnlyList<string> FailedChecks)
{
    public static OperationalReadinessResult Ready { get; } = new(true, []);
}

public sealed class OperationalReadinessChecker : IOperationalReadinessChecker
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly JwtSigningKeyStore _jwtKeys;

    public OperationalReadinessChecker(
        AppDbContext db,
        IConfiguration configuration,
        JwtSigningKeyStore jwtKeys)
    {
        _db = db;
        _configuration = configuration;
        _jwtKeys = jwtKeys;
    }

    public async Task<OperationalReadinessResult> CheckAsync(CancellationToken ct)
    {
        List<string> failed = [];

        if (!await CanConnectToDatabaseAsync(ct))
        {
            failed.Add("database");
        }

        if (!HasRequiredSecret("Csrf:SigningKey", "Csrf:SigningKeyFile"))
        {
            failed.Add("csrf_signing_key");
        }

        if (!HasRequiredSecret("InternalService:Token", "InternalService:TokenFile"))
        {
            failed.Add("internal_service_token");
        }

        try
        {
            _jwtKeys.GetCurrentSigningKey();
        }
        catch (InvalidOperationException)
        {
            failed.Add("jwt_signing_keys");
        }

        return failed.Count == 0
            ? OperationalReadinessResult.Ready
            : new OperationalReadinessResult(false, failed);
    }

    private async Task<bool> CanConnectToDatabaseAsync(CancellationToken ct)
    {
        try
        {
            return await _db.Database.CanConnectAsync(ct);
        }
        catch
        {
            return false;
        }
    }

    private bool HasRequiredSecret(string valueKey, string fileKey)
    {
        string? direct = _configuration[valueKey];
        if (!string.IsNullOrWhiteSpace(direct))
        {
            return true;
        }

        string? file = _configuration[fileKey];
        return !string.IsNullOrWhiteSpace(file) && File.Exists(file) && new FileInfo(file).Length > 0;
    }
}
