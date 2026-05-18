using AdvancedRag.App.Reporting;
using AdvancedRag.Infrastructure.Reporting;
using FluentAssertions;

namespace AdvancedRag.Infrastructure.Tests;

public sealed class NpgsqlFeedbackReportingServiceTests
{
    [Fact]
    public void BuildSql_WithoutOptionalFilters_SeparatesPredicateAndOrdering()
    {
        FeedbackReportQuery query = new(
            NegativeOnly: false,
            CitedDocumentId: null,
            UserId: null,
            From: null,
            To: null);

        string sql = NpgsqlFeedbackReportingService.BuildSql(query);

        sql.Should().Contain("where event.feedback_value is not null");
        sql.Should().Contain("order by event.feedback_updated_at desc");
        sql.Should().NotContain("nullorder");
    }
}
