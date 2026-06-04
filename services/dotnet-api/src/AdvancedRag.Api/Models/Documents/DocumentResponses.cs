using AdvancedRag.App.Documents;

namespace AdvancedRag.Api.Models.Documents;

public sealed record DocumentSummaryResponse(
    Guid Id,
    string Title,
    string State,
    string DocumentType,
    string Audience,
    IReadOnlyList<Guid> AllowedGroupIds,
    int? DraftVersionNumber,
    int? PublishedVersionNumber,
    string IndexingStatus,
    DateTimeOffset UpdatedAt)
{
    public static DocumentSummaryResponse FromSummary(DocumentSummary summary)
    {
        return new DocumentSummaryResponse(
            summary.Id,
            summary.Title,
            ToDisplay(summary.State),
            summary.DocumentType,
            summary.Audience,
            summary.AllowedGroupIds,
            summary.DraftVersionNumber,
            summary.PublishedVersionNumber,
            summary.IndexingStatus.ToString(),
            summary.UpdatedAt);
    }

    private static string ToDisplay(DocumentState state)
    {
        return state == DocumentState.InReview ? "In Review" : state.ToString();
    }
}

public sealed record DocumentDetailResponse(
    Guid Id,
    string Title,
    string State,
    DocumentVersionResponse? CurrentDraftVersion,
    DocumentVersionResponse? CurrentPublishedVersion,
    IReadOnlyList<Guid> AllowedGroupIds,
    DateTimeOffset UpdatedAt)
{
    public static DocumentDetailResponse FromAggregate(DocumentAggregate document)
    {
        return new DocumentDetailResponse(
            document.Id,
            document.Title,
            ToDisplay(document.State),
            document.CurrentDraftVersion is null ? null : DocumentVersionResponse.FromVersion(document.CurrentDraftVersion),
            document.CurrentPublishedVersion is null ? null : DocumentVersionResponse.FromVersion(document.CurrentPublishedVersion),
            document.AllowedGroupIds,
            document.UpdatedAt);
    }

    private static string ToDisplay(DocumentState state)
    {
        return state == DocumentState.InReview ? "In Review" : state.ToString();
    }
}

public sealed record DocumentVersionResponse(
    Guid Id,
    int VersionNumber,
    string State,
    string Title,
    string DocumentType,
    string Audience,
    string ContentHtml,
    string IndexingStatus)
{
    public static DocumentVersionResponse FromVersion(DocumentVersionRecord version)
    {
        return new DocumentVersionResponse(
            version.Id,
            version.VersionNumber,
            version.State == DocumentVersionState.InReview ? "In Review" : version.State.ToString(),
            version.Title,
            version.DocumentType,
            version.Audience,
            version.ContentHtml,
            version.IndexingStatus.ToString());
    }
}

public sealed record ImportExtractionResponse(
    string Text,
    string? ContentHtml,
    ImportExtractionMetadataResponse Metadata)
{
    public static ImportExtractionResponse FromResult(ImportExtractionResult result)
    {
        return new ImportExtractionResponse(
            result.Text,
            result.ContentHtml,
            new ImportExtractionMetadataResponse(
                result.Metadata.OriginalFilename,
                result.Metadata.MimeType,
                result.Metadata.SizeBytes,
                result.Metadata.Sha256Hash,
                result.Metadata.ExtractionStatus));
    }
}

public sealed record ImportExtractionMetadataResponse(
    string OriginalFilename,
    string MimeType,
    long SizeBytes,
    string Sha256Hash,
    string ExtractionStatus);
