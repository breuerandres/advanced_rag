namespace AdvancedRag.App.Documents;

public sealed class DocumentTypeService : IDocumentTypeService
{
    private const int MaxNameLength = 80;

    private readonly IDocumentTypeRepository _repository;

    public DocumentTypeService(IDocumentTypeRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<DocumentTypeRecord>> ListAsync(bool includeInactive, CancellationToken ct)
    {
        return _repository.ListAsync(includeInactive, ct);
    }

    public async Task<DocumentTypeRecord> CreateAsync(CreateDocumentTypeCommand command, CancellationToken ct)
    {
        string name = NormalizeName(command.Name);
        await RequireUniqueNameAsync(name, null, ct);
        int sortOrder = command.SortOrder ?? 0;
        return await _repository.CreateAsync(name, sortOrder, command.ActorUserId, ct);
    }

    public async Task<DocumentTypeRecord> UpdateAsync(UpdateDocumentTypeCommand command, CancellationToken ct)
    {
        string? name = command.Name is null ? null : NormalizeName(command.Name);
        if (name is not null)
        {
            await RequireUniqueNameAsync(name, command.Id, ct);
        }

        DocumentTypeRecord? updated = await _repository.UpdateAsync(
            command.Id,
            name,
            command.IsActive,
            command.SortOrder,
            command.ActorUserId,
            ct);

        return updated
            ?? throw new DocumentTypeException("NOT_FOUND", 404, "Document type not found.");
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        DocumentTypeDeletionOutcome outcome = await _repository.DeleteAsync(id, ct);
        switch (outcome)
        {
            case DocumentTypeDeletionOutcome.Deleted:
                return;
            case DocumentTypeDeletionOutcome.NotFound:
                throw new DocumentTypeException("NOT_FOUND", 404, "Document type not found.");
            case DocumentTypeDeletionOutcome.InUse:
                throw new DocumentTypeException(
                    "DOCUMENT_TYPE_IN_USE",
                    409,
                    "Document type is in use by one or more documents and cannot be deleted. Deactivate it instead.",
                    new Dictionary<string, object?> { ["field"] = "id" });
            default:
                throw new DocumentTypeException("INTERNAL_ERROR", 500, "Unexpected document-type deletion outcome.");
        }
    }

    private async Task RequireUniqueNameAsync(string name, Guid? excludingId, CancellationToken ct)
    {
        if (await _repository.NameExistsAsync(name, excludingId, ct))
        {
            throw new DocumentTypeException(
                "CONFLICT",
                409,
                "A document type with this name already exists.",
                new Dictionary<string, object?> { ["field"] = "name" });
        }
    }

    private static string NormalizeName(string value)
    {
        string normalized = value.Trim();
        if (normalized.Length == 0)
        {
            throw new DocumentTypeException(
                "VALIDATION_FAILED",
                400,
                "Name is required.",
                new Dictionary<string, object?> { ["field"] = "name" });
        }

        if (normalized.Length > MaxNameLength)
        {
            throw new DocumentTypeException(
                "VALIDATION_FAILED",
                400,
                $"Name must be at most {MaxNameLength} characters.",
                new Dictionary<string, object?> { ["field"] = "name" });
        }

        return normalized;
    }
}
