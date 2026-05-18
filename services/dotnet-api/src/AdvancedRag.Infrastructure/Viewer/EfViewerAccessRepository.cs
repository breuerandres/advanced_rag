using AdvancedRag.App.Viewer;
using AdvancedRag.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdvancedRag.Infrastructure.Viewer;

public sealed class EfViewerAccessRepository : IViewerAccessRepository
{
    private readonly AppDbContext _db;

    public EfViewerAccessRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ViewerInstructionAccess?> FindInstructionAsync(Guid instructionId, CancellationToken ct)
    {
        Instruction? instruction = await _db.Instructions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == instructionId, ct);

        if (instruction is null)
        {
            return null;
        }

        ViewerInstructionVersion? draft = instruction.CurrentDraftVersionId is null
            ? null
            : await FindVersionAsync(instruction.CurrentDraftVersionId.Value, ct);
        ViewerInstructionVersion? published = instruction.CurrentPublishedVersionId is null
            ? null
            : await FindVersionAsync(instruction.CurrentPublishedVersionId.Value, ct);

        return new ViewerInstructionAccess(
            instruction.Id,
            instruction.Title,
            instruction.CurrentState,
            draft,
            published);
    }

    public async Task SaveExchangeCodeAsync(ViewerExchangeCodeRecord code, CancellationToken ct)
    {
        _db.ViewerExchangeCodes.Add(new ViewerExchangeCode
        {
            Id = code.Id,
            CodeHash = code.CodeHash,
            InstructionId = code.InstructionId,
            UserId = code.UserId,
            Purpose = code.Purpose,
            AllowedStatuses = code.AllowedStatuses,
            ExpiresAt = code.ExpiresAt,
            ConsumedAt = code.ConsumedAt,
            CreatedAt = code.CreatedAt,
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<ViewerExchangeCodeRecord?> FindExchangeCodeByHashAsync(string codeHash, CancellationToken ct)
    {
        ViewerExchangeCode? code = await _db.ViewerExchangeCodes
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.CodeHash == codeHash, ct);
        return code is null ? null : ToRecord(code);
    }

    public async Task MarkExchangeCodeConsumedAsync(Guid exchangeCodeId, DateTimeOffset consumedAt, CancellationToken ct)
    {
        ViewerExchangeCode code = await _db.ViewerExchangeCodes.SingleAsync(item => item.Id == exchangeCodeId, ct);
        code.ConsumedAt = consumedAt;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SaveTokenAuditAsync(ViewerTokenAuditRecord audit, CancellationToken ct)
    {
        _db.ViewerTokenAudit.Add(new ViewerTokenAudit
        {
            Id = audit.Id,
            ViewerTokenId = audit.ViewerTokenId,
            InstructionId = audit.InstructionId,
            UserId = audit.UserId,
            Purpose = audit.Purpose,
            IssuedAt = audit.IssuedAt,
            ExpiresAt = audit.ExpiresAt,
        });
        await _db.SaveChangesAsync(ct);
    }

    private async Task<ViewerInstructionVersion?> FindVersionAsync(Guid versionId, CancellationToken ct)
    {
        InstructionVersion? version = await _db.InstructionVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == versionId, ct);
        return version is null
            ? null
            : new ViewerInstructionVersion(
                version.Id,
                version.VersionNumber,
                version.State,
                version.Title,
                version.InstructionType,
                version.Audience,
                version.ContentHtml);
    }

    private static ViewerExchangeCodeRecord ToRecord(ViewerExchangeCode code)
    {
        return new ViewerExchangeCodeRecord(
            code.Id,
            code.CodeHash,
            code.InstructionId,
            code.UserId,
            code.Purpose,
            code.AllowedStatuses,
            code.ExpiresAt,
            code.ConsumedAt,
            code.CreatedAt);
    }
}
