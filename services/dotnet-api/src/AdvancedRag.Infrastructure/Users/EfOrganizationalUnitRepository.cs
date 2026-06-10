using AdvancedRag.App.Users;
using AdvancedRag.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdvancedRag.Infrastructure.Users;

public sealed class EfOrganizationalUnitRepository : IOrganizationalUnitRepository
{
    private readonly AppDbContext _db;

    public EfOrganizationalUnitRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<OrganizationalUnitRecord>> ListTreeAsync(bool includeInactive, CancellationToken ct)
    {
        return await BuildRecordsAsync(activeOnly: !includeInactive, ct);
    }

    public async Task<OrganizationalUnitRecord?> FindAsync(Guid id, CancellationToken ct)
    {
        IReadOnlyList<OrganizationalUnitRecord> records = await BuildRecordsAsync(activeOnly: false, ct);
        return records.SingleOrDefault(item => item.Id == id);
    }

    public async Task<OrganizationalUnitRecord> CreateAsync(
        string name,
        Guid parentId,
        Guid actorUserId,
        CancellationToken ct)
    {
        OrganizationalUnit unit = new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            ParentId = parentId,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _db.OrganizationalUnits.Add(unit);
        _db.OrganizationalUnitClosure.Add(new OrganizationalUnitClosure
        {
            AncestorId = unit.Id,
            DescendantId = unit.Id,
            Depth = 0,
        });

        OrganizationalUnitClosure[] ancestorRows = await _db.OrganizationalUnitClosure
            .AsNoTracking()
            .Where(closure => closure.DescendantId == parentId)
            .ToArrayAsync(ct);
        _db.OrganizationalUnitClosure.AddRange(ancestorRows.Select(closure => new OrganizationalUnitClosure
        {
            AncestorId = closure.AncestorId,
            DescendantId = unit.Id,
            Depth = closure.Depth + 1,
        }));

        await _db.SaveChangesAsync(ct);
        return (await FindAsync(unit.Id, ct))!;
    }

    public async Task<OrganizationalUnitRecord?> UpdateAsync(
        Guid id,
        string? name,
        bool? isActive,
        Guid actorUserId,
        CancellationToken ct)
    {
        OrganizationalUnit? unit = await _db.OrganizationalUnits.SingleOrDefaultAsync(item => item.Id == id, ct);
        if (unit is null)
        {
            return null;
        }

        if (name is not null)
        {
            unit.Name = name;
        }

        if (isActive is not null)
        {
            unit.IsActive = isActive.Value;
        }

        unit.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await FindAsync(id, ct);
    }

    private async Task<IReadOnlyList<OrganizationalUnitRecord>> BuildRecordsAsync(
        bool activeOnly,
        CancellationToken ct)
    {
        var units = await _db.OrganizationalUnits
            .AsNoTracking()
            .Where(unit => !activeOnly || unit.IsActive)
            .OrderBy(unit => unit.Name)
            .ToListAsync(ct);
        Guid[] unitIds = units.Select(unit => unit.Id).ToArray();
        var depths = await _db.OrganizationalUnitClosure
            .AsNoTracking()
            .Where(closure => unitIds.Contains(closure.DescendantId))
            .GroupBy(closure => closure.DescendantId)
            .Select(group => new { Id = group.Key, Depth = group.Max(closure => closure.Depth) })
            .ToDictionaryAsync(item => item.Id, item => item.Depth, ct);

        return units
            .Select(unit => new OrganizationalUnitRecord(
                unit.Id,
                unit.Name,
                unit.ParentId,
                depths.GetValueOrDefault(unit.Id),
                unit.IsActive))
            .OrderBy(unit => unit.Depth)
            .ThenBy(unit => unit.Name, StringComparer.Ordinal)
            .ToArray();
    }
}
