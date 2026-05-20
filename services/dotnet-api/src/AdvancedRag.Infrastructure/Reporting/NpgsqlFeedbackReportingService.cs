using System.Text;
using AdvancedRag.App.Reporting;
using Npgsql;

namespace AdvancedRag.Infrastructure.Reporting;

public sealed class NpgsqlFeedbackReportingService : IFeedbackReportingService
{
    private readonly string _connectionString;

    public NpgsqlFeedbackReportingService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IReadOnlyList<FeedbackReportItem>> ListFeedbackAsync(
        FeedbackReportQuery query,
        CancellationToken ct)
    {
        await using NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync(ct);

        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = BuildSql(query);
        AddParameters(command, query);

        Dictionary<Guid, FeedbackReportBuilder> builders = new();
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            Guid auditId = reader.GetGuid(reader.GetOrdinal("query_audit_event_id"));
            if (!builders.TryGetValue(auditId, out FeedbackReportBuilder? builder))
            {
                builder = new FeedbackReportBuilder(
                    auditId,
                    reader.GetGuid(reader.GetOrdinal("user_id")),
                    reader.GetString(reader.GetOrdinal("user_display_name")),
                    reader.GetString(reader.GetOrdinal("question")),
                    reader.GetString(reader.GetOrdinal("answer_summary")),
                    reader.GetString(reader.GetOrdinal("feedback_value")),
                    reader.IsDBNull(reader.GetOrdinal("feedback_comment"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("feedback_comment")),
                    reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("feedback_updated_at")),
                    reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at")),
                    reader.GetBoolean(reader.GetOrdinal("cache_hit")),
                    reader.GetString(reader.GetOrdinal("request_id")));
                builders.Add(auditId, builder);
            }

            if (!reader.IsDBNull(reader.GetOrdinal("document_id")))
            {
                builder.Citations.Add(
                    new FeedbackReportCitation(
                        reader.GetGuid(reader.GetOrdinal("document_id")),
                        reader.GetGuid(reader.GetOrdinal("document_version_id")),
                        reader.GetFieldValue<string[]>(reader.GetOrdinal("heading_path"))));
            }
        }

        return builders.Values.Select(builder => builder.Build()).ToList();
    }

    internal static string BuildSql(FeedbackReportQuery query)
    {
        StringBuilder sql = new(
            """
            select
                event.query_audit_event_id,
                event.user_id,
                event.user_id::text as user_display_name,
                event.question,
                event.answer_summary,
                event.feedback_value,
                event.feedback_comment,
                event.feedback_updated_at,
                event.created_at,
                event.cache_hit,
                event.request_id,
                event.document_id,
                event.document_version_id,
                event.heading_path
            from rag.v_query_audit_with_citations event
            where event.feedback_value is not null
            """);

        if (query.NegativeOnly)
        {
            sql.AppendLine("and event.feedback_value = 'down'");
        }

        if (query.UserId is not null)
        {
            sql.AppendLine("and event.user_id = @user_id");
        }

        if (query.CitedDocumentId is not null)
        {
            sql.AppendLine(
                """
                and exists (
                    select 1
                    from rag.v_query_audit_with_citations filter_citation
                    where filter_citation.query_audit_event_id = event.query_audit_event_id
                      and filter_citation.document_id = @cited_document_id
                )
                """);
        }

        if (query.From is not null)
        {
            sql.AppendLine("and event.created_at >= @from");
        }

        if (query.To is not null)
        {
            sql.AppendLine("and event.created_at < @to");
        }

        sql.AppendLine();
        sql.AppendLine("order by event.feedback_updated_at desc, event.created_at desc, event.citation_created_at asc");
        return sql.ToString();
    }

    private static void AddParameters(NpgsqlCommand command, FeedbackReportQuery query)
    {
        if (query.UserId is not null)
        {
            command.Parameters.AddWithValue("user_id", query.UserId.Value);
        }

        if (query.CitedDocumentId is not null)
        {
            command.Parameters.AddWithValue("cited_document_id", query.CitedDocumentId.Value);
        }

        if (query.From is not null)
        {
            command.Parameters.AddWithValue("from", query.From.Value);
        }

        if (query.To is not null)
        {
            command.Parameters.AddWithValue("to", query.To.Value);
        }
    }

    private sealed class FeedbackReportBuilder
    {
        private readonly Guid _queryAuditEventId;
        private readonly Guid _userId;
        private readonly string _userDisplayName;
        private readonly string _question;
        private readonly string _answerSummary;
        private readonly string _feedbackValue;
        private readonly string? _feedbackComment;
        private readonly DateTimeOffset _feedbackUpdatedAt;
        private readonly DateTimeOffset _createdAt;
        private readonly bool _cacheHit;
        private readonly string _requestId;

        public FeedbackReportBuilder(
            Guid queryAuditEventId,
            Guid userId,
            string userDisplayName,
            string question,
            string answerSummary,
            string feedbackValue,
            string? feedbackComment,
            DateTimeOffset feedbackUpdatedAt,
            DateTimeOffset createdAt,
            bool cacheHit,
            string requestId)
        {
            _queryAuditEventId = queryAuditEventId;
            _userId = userId;
            _userDisplayName = userDisplayName;
            _question = question;
            _answerSummary = answerSummary;
            _feedbackValue = feedbackValue;
            _feedbackComment = feedbackComment;
            _feedbackUpdatedAt = feedbackUpdatedAt;
            _createdAt = createdAt;
            _cacheHit = cacheHit;
            _requestId = requestId;
        }

        public List<FeedbackReportCitation> Citations { get; } = [];

        public FeedbackReportItem Build()
        {
            return new FeedbackReportItem(
                _queryAuditEventId,
                _userId,
                _userDisplayName,
                _question,
                _answerSummary,
                _feedbackValue,
                _feedbackComment,
                _feedbackUpdatedAt,
                _createdAt,
                _cacheHit,
                _requestId,
                Citations);
        }
    }
}
