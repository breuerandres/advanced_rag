namespace AdvancedRag.App.Reporting;

public sealed record FeedbackReportQuery(
    bool NegativeOnly,
    Guid? CitedDocumentId,
    Guid? UserId,
    DateTimeOffset? From,
    DateTimeOffset? To);

public sealed record FeedbackReportItem(
    Guid QueryAuditEventId,
    Guid UserId,
    string UserDisplayName,
    string Question,
    string AnswerSummary,
    string FeedbackValue,
    string? FeedbackComment,
    DateTimeOffset FeedbackUpdatedAt,
    DateTimeOffset CreatedAt,
    bool CacheHit,
    string RequestId,
    IReadOnlyList<FeedbackReportCitation> Citations);

public sealed record FeedbackReportCitation(
    Guid InstructionId,
    Guid InstructionVersionId,
    IReadOnlyList<string> HeadingPath);

public interface IFeedbackReportingService
{
    Task<IReadOnlyList<FeedbackReportItem>> ListFeedbackAsync(
        FeedbackReportQuery query,
        CancellationToken ct);
}
