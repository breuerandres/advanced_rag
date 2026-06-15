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

        sql.Should().Contain("from rag.v_query_audit_with_citations event");
        sql.Should().Contain("left join app.users account on account.\"Id\" = event.user_id");
        sql.Should().Contain("coalesce(account.display_name, event.user_id::text) as user_display_name");
        sql.Should().Contain("account.email as user_email");
        sql.Should().Contain("where true");
        sql.Should().NotContain("where event.feedback_value is not null");
        sql.Should().Contain("order by event.feedback_updated_at desc nulls last");
        sql.Should().NotContain("nullorder");
    }

    [Fact]
    public void BuildSql_WithNegativeOnly_FiltersToNegativeFeedbackRows()
    {
        FeedbackReportQuery query = new(
            NegativeOnly: true,
            CitedDocumentId: null,
            UserId: null,
            From: null,
            To: null);

        string sql = NpgsqlFeedbackReportingService.BuildSql(query);

        sql.Should().Contain("and event.feedback_value = 'down'");
    }
}
