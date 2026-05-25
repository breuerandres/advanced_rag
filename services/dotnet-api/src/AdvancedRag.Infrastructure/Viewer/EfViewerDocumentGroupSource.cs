using AdvancedRag.App.Users;
using AdvancedRag.App.Viewer;
using AdvancedRag.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdvancedRag.Infrastructure.Viewer;

public sealed class EfViewerDocumentGroupSource : IViewerDocumentGroupSource
{
    private readonly AppDbContext _db;

    public EfViewerDocumentGroupSource(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<GroupRecord>> ListGroupsAsync(CancellationToken ct)
    {
        return await _db.Groups
            .AsNoTracking()
            .OrderBy(group => group.Name)
            .Select(group => new GroupRecord(group.Id, group.Name))
            .ToListAsync(ct);
    }
}
