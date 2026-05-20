using AdvancedRag.Api.Models.Reporting;
using AdvancedRag.App.Reporting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdvancedRag.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,DocumentManager")]
[Route("api/reporting")]
public sealed class ReportingController : ApiControllerBase
{
    private readonly IFeedbackReportingService _feedbackReporting;

    public ReportingController(IFeedbackReportingService feedbackReporting)
    {
        _feedbackReporting = feedbackReporting;
    }

    [HttpGet("feedback")]
    public async Task<ActionResult<IReadOnlyList<FeedbackReportItemResponse>>> ListFeedback(
        [FromQuery] bool negativeOnly,
        [FromQuery] Guid? citedDocumentId,
        [FromQuery] Guid? userId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        FeedbackReportQuery query = new(
            negativeOnly,
            citedDocumentId,
            userId,
            from,
            to);
        IReadOnlyList<FeedbackReportItem> items = await _feedbackReporting.ListFeedbackAsync(query, ct);
        return Ok(items.Select(ToResponse).ToList());
    }

    private static FeedbackReportItemResponse ToResponse(FeedbackReportItem item)
    {
        return new FeedbackReportItemResponse(
            item.QueryAuditEventId,
            item.UserId,
            item.UserDisplayName,
            item.Question,
            item.AnswerSummary,
            item.FeedbackValue,
            item.FeedbackComment,
            item.FeedbackUpdatedAt,
            item.CreatedAt,
            item.CacheHit,
            item.RequestId,
            item.Citations
                .Select(citation => new FeedbackReportCitationResponse(
                    citation.DocumentId,
                    citation.DocumentVersionId,
                    citation.HeadingPath))
                .ToList());
    }
}
