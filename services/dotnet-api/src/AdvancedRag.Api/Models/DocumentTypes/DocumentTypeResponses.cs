using AdvancedRag.App.Documents;

namespace AdvancedRag.Api.Models.DocumentTypes;

public sealed record DocumentTypeResponse(
    Guid Id,
    string Name,
    bool IsActive,
    int SortOrder)
{
    public static DocumentTypeResponse FromRecord(DocumentTypeRecord record)
    {
        return new DocumentTypeResponse(record.Id, record.Name, record.IsActive, record.SortOrder);
    }
}
