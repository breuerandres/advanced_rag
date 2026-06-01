using AdvancedRag.App.DocumentImages;
using AdvancedRag.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdvancedRag.Infrastructure.DocumentImages;

public sealed class EfDocumentImageRepository : IDocumentImageRepository
{
    private readonly AppDbContext _db;

    public EfDocumentImageRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(DocumentImageRecord image, CancellationToken ct)
    {
        _db.DocumentImages.Add(new DocumentImage
        {
            Id = image.Id,
            DocumentId = image.DocumentId,
            ObjectKey = image.ObjectKey,
            OriginalFilename = image.OriginalFilename,
            ContentType = image.ContentType,
            SizeBytes = image.SizeBytes,
            Sha256Hash = image.Sha256Hash,
            AltText = image.AltText,
            UploadedByUserId = image.UploadedByUserId,
            CreatedAt = image.CreatedAt,
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<DocumentImageRecord?> FindAsync(Guid imageId, CancellationToken ct)
    {
        DocumentImage? image = await _db.DocumentImages
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == imageId, ct);

        return image is null
            ? null
            : new DocumentImageRecord(
                image.Id,
                image.DocumentId,
                image.ObjectKey,
                image.OriginalFilename,
                image.ContentType,
                image.SizeBytes,
                image.Sha256Hash,
                image.AltText,
                image.UploadedByUserId,
                image.CreatedAt);
    }
}
