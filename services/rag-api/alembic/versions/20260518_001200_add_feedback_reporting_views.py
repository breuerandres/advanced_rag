"""add feedback reporting views"""

from collections.abc import Sequence

from alembic import op

revision: str = "20260518_001200"
down_revision: str | None = "20260513_184500"
branch_labels: str | Sequence[str] | None = None
depends_on: str | Sequence[str] | None = None


def upgrade() -> None:
    op.execute(
        """
        CREATE VIEW rag.v_query_audit_with_citations AS
        SELECT
            event.id AS query_audit_event_id,
            event.user_id,
            event.question,
            left(event.answer, 240) AS answer_summary,
            event.feedback_value,
            event.feedback_comment,
            event.feedback_updated_at,
            event.created_at,
            event.cache_hit,
            event.request_id,
            citation.document_id,
            citation.document_version_id,
            citation.heading_path,
            citation.created_at AS citation_created_at
        FROM rag.query_audit_events event
        LEFT JOIN rag.query_audit_citations citation
            ON citation.query_audit_event_id = event.id
        """
    )
    op.execute(
        """
        CREATE VIEW rag.v_feedback_summary AS
        SELECT
            query_audit_event_id,
            user_id,
            question,
            answer_summary,
            feedback_value,
            feedback_comment,
            feedback_updated_at,
            created_at,
            cache_hit,
            request_id
        FROM rag.v_query_audit_with_citations
        WHERE feedback_value IS NOT NULL
        GROUP BY
            query_audit_event_id,
            user_id,
            question,
            answer_summary,
            feedback_value,
            feedback_comment,
            feedback_updated_at,
            created_at,
            cache_hit,
            request_id
        """
    )
    op.execute(
        """
        DO $$
        BEGIN
            IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_reporting_reader') THEN
                GRANT SELECT ON rag.v_query_audit_with_citations TO app_reporting_reader;
                GRANT SELECT ON rag.v_feedback_summary TO app_reporting_reader;
            END IF;
        END
        $$;
        """
    )


def downgrade() -> None:
    op.execute("DROP VIEW IF EXISTS rag.v_feedback_summary")
    op.execute("DROP VIEW IF EXISTS rag.v_query_audit_with_citations")
