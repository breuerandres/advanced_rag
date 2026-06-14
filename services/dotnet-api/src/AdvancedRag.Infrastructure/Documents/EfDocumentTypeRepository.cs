using AdvancedRag.App.Documents;
using AdvancedRag.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdvancedRag.Infrastructure.Documents;

public sealed class EfDocumentTypeRepository : IDocumentTypeRepository
{
    private readonly AppDbContext _db;

    public EfDocumentTypeRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<DocumentTypeRecord>> ListAsync(bool includeInactive, CancellationToken ct)
    {
        return await _db.DocumentTypes
            .AsNoTracking()
            .Where(type => includeInactive || type.IsActive)
            .OrderBy(type => type.SortOrder)
            .ThenBy(type => type.Name)
            .Select(type => new DocumentTypeRecord(type.Id, type.Name, type.IsActive, type.SortOrder))
            .ToArrayAsync(ct);
    }

    public async Task<DocumentTypeRecord?> FindAsync(Guid id, CancellationToken ct)
    {
        return await _db.DocumentTypes
            .AsNoTracking()
            .Where(type => type.Id == id)
            .Select(type => new DocumentTypeRecord(type.Id, type.Name, type.IsActive, type.SortOrder))
            .SingleOrDefaultAsync(ct);
    }

    public async Task<bool> NameExistsAsync(string name, Guid? excludingId, CancellationToken ct)
    {
        string normalized = name.Trim().ToLower();
        return await _db.DocumentTypes
            .AsNoTracking()
            .AnyAsync(
                type => type.Name.ToLower() == normalized && (excludingId == null || type.Id != excludingId),
                ct);
    }

    public async Task<DocumentTypeRecord> CreateAsync(string name, int sortOrder, Guid actorUserId, CancellationToken ct)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DocumentType type = new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            IsActive = true,
            SortOrder = sortOrder,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.DocumentTypes.Add(type);
        await _db.SaveChangesAsync(ct);
        return new DocumentTypeRecord(type.Id, type.Name, type.IsActive, type.SortOrder);
    }

    public async Task<DocumentTypeRecord?> UpdateAsync(
        Guid id,
        string? name,
        bool? isActive,
        int? sortOrder,
        Guid actorUserId,
        CancellationToken ct)
    {
        DocumentType? type = await _db.DocumentTypes.SingleOrDefaultAsync(item => item.Id == id, ct);
        if (type is null)
        {
            return null;
        }

        if (name is not null)
        {
            type.Name = name;
        }

        if (isActive is not null)
        {
            type.IsActive = isActive.Value;
        }

        if (sortOrder is not null)
        {
            type.SortOrder = sortOrder.Value;
        }

        type.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return new DocumentTypeRecord(type.Id, type.Name, type.IsActive, type.SortOrder);
    }

    public async Task<DocumentTypeDeletionOutcome> DeleteAsync(Guid id, CancellationToken ct)
    {
        DocumentType? type = await _db.DocumentTypes.SingleOrDefaultAsync(item => item.Id == id, ct);
        if (type is null)
        {
            return DocumentTypeDeletionOutcome.NotFound;
        }

        bool inUse = await _db.DocumentVersions.AnyAsync(version => version.DocumentTypeId == id, ct);
        if (inUse)
        {
            return DocumentTypeDeletionOutcome.InUse;
        }

        _db.DocumentTypes.Remove(type);
        await _db.SaveChangesAsync(ct);
        return DocumentTypeDeletionOutcome.Deleted;
    }
}
