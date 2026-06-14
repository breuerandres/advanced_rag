namespace AdvancedRag.App.Documents;

public sealed record DocumentTypeRecord(
    Guid Id,
    string Name,
    bool IsActive,
    int SortOrder);

public sealed record CreateDocumentTypeCommand(
    string Name,
    int? SortOrder,
    Guid ActorUserId);

public sealed record UpdateDocumentTypeCommand(
    Guid Id,
    string? Name,
    bool? IsActive,
    int? SortOrder,
    Guid ActorUserId);

public enum DocumentTypeDeletionOutcome
{
    Deleted,
    NotFound,
    InUse,
}

public interface IDocumentTypeRepository
{
    Task<IReadOnlyList<DocumentTypeRecord>> ListAsync(bool includeInactive, CancellationToken ct);

    Task<DocumentTypeRecord?> FindAsync(Guid id, CancellationToken ct);

    Task<bool> NameExistsAsync(string name, Guid? excludingId, CancellationToken ct);

    Task<DocumentTypeRecord> CreateAsync(string name, int sortOrder, Guid actorUserId, CancellationToken ct);

    Task<DocumentTypeRecord?> UpdateAsync(
        Guid id,
        string? name,
        bool? isActive,
        int? sortOrder,
        Guid actorUserId,
        CancellationToken ct);

    Task<DocumentTypeDeletionOutcome> DeleteAsync(Guid id, CancellationToken ct);
}

public interface IDocumentTypeService
{
    Task<IReadOnlyList<DocumentTypeRecord>> ListAsync(bool includeInactive, CancellationToken ct);

    Task<DocumentTypeRecord> CreateAsync(CreateDocumentTypeCommand command, CancellationToken ct);

    Task<DocumentTypeRecord> UpdateAsync(UpdateDocumentTypeCommand command, CancellationToken ct);

    Task DeleteAsync(Guid id, CancellationToken ct);
}

public sealed class DocumentTypeException : Exception
{
    public DocumentTypeException(
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
