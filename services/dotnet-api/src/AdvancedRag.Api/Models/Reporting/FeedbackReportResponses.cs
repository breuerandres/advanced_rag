namespace AdvancedRag.Api.Models.Reporting;

public sealed record FeedbackReportItemResponse(
    Guid QueryAuditEventId,
    Guid UserId,
    string UserDisplayName,
    string Question,
    string AnswerSummary,
    string? FeedbackValue,
    string? FeedbackComment,
    DateTimeOffset? FeedbackUpdatedAt,
    DateTimeOffset CreatedAt,
    bool CacheHit,
    string RequestId,
    IReadOnlyList<FeedbackReportCitationResponse> Citations);

public sealed record FeedbackReportCitationResponse(
    Guid DocumentId,
    Guid DocumentVersionId,
    IReadOnlyList<string> HeadingPath);
