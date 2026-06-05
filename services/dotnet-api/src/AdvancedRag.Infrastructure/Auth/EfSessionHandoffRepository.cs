using AdvancedRag.App.Auth;
using AdvancedRag.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdvancedRag.Infrastructure.Auth;

public sealed class EfSessionHandoffRepository : ISessionHandoffRepository
{
    private readonly AppDbContext _db;

    public EfSessionHandoffRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task StoreAsync(SessionHandoffRecord record, CancellationToken ct)
    {
        _db.SessionHandoffCodes.Add(new SessionHandoffCode
        {
            Id = record.Id,
            CodeHash = record.CodeHash,
            UserId = record.UserId,
            Target = record.Target,
            ExpiresAt = record.ExpiresAt,
            ConsumedAt = record.ConsumedAt,
            CreatedAt = record.CreatedAt,
            RequestId = record.RequestId,
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<SessionHandoffRecord?> FindByCodeHashAsync(string codeHash, CancellationToken ct)
    {
        SessionHandoffCode? record = await _db.SessionHandoffCodes
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.CodeHash == codeHash, ct);
        return record is null
            ? null
            : new SessionHandoffRecord(
                record.Id,
                record.CodeHash,
                record.UserId,
                record.Target,
                record.ExpiresAt,
                record.ConsumedAt,
                record.CreatedAt,
                record.RequestId);
    }

    public async Task MarkConsumedAsync(Guid id, DateTimeOffset consumedAt, CancellationToken ct)
    {
        await _db.SessionHandoffCodes
            .Where(item => item.Id == id && item.ConsumedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.ConsumedAt, consumedAt),
                ct);
    }
}
