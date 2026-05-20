"""rename instruction column vocabulary to documents"""

from collections.abc import Sequence

from alembic import op

revision: str = "20260520_170000"
down_revision: str | None = "20260518_001200"
branch_labels: str | Sequence[str] | None = None
depends_on: str | Sequence[str] | None = None


def upgrade() -> None:
    op.execute("DROP VIEW IF EXISTS rag.v_feedback_summary")
    op.execute("DROP VIEW IF EXISTS rag.v_query_audit_with_citations")
    op.execute(
        """
        DO $$
        BEGIN
            IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'rag' AND table_name = 'indexing_jobs' AND column_name = 'instruction_id') THEN
                ALTER TABLE rag.indexing_jobs RENAME COLUMN instruction_id TO document_id;
            END IF;
            IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'rag' AND table_name = 'indexing_jobs' AND column_name = 'instruction_version_id') THEN
                ALTER TABLE rag.indexing_jobs RENAME COLUMN instruction_version_id TO document_version_id;
            END IF;
            IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'rag' AND table_name = 'document_chunks' AND column_name = 'instruction_id') THEN
                ALTER TABLE rag.document_chunks RENAME COLUMN instruction_id TO document_id;
            END IF;
            IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'rag' AND table_name = 'document_chunks' AND column_name = 'instruction_version_id') THEN
                ALTER TABLE rag.document_chunks RENAME COLUMN instruction_version_id TO document_version_id;
            END IF;
            IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'rag' AND table_name = 'semantic_cache_sources' AND column_name = 'instruction_id') THEN
                ALTER TABLE rag.semantic_cache_sources RENAME COLUMN instruction_id TO document_id;
            END IF;
            IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'rag' AND table_name = 'semantic_cache_sources' AND column_name = 'instruction_version_id') THEN
                ALTER TABLE rag.semantic_cache_sources RENAME COLUMN instruction_version_id TO document_version_id;
            END IF;
            IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'rag' AND table_name = 'query_audit_citations' AND column_name = 'instruction_id') THEN
                ALTER TABLE rag.query_audit_citations RENAME COLUMN instruction_id TO document_id;
            END IF;
            IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'rag' AND table_name = 'query_audit_citations' AND column_name = 'instruction_version_id') THEN
                ALTER TABLE rag.query_audit_citations RENAME COLUMN instruction_version_id TO document_version_id;
            END IF;
        END $$;
        """
    )
    op.execute(
        "ALTER INDEX IF EXISTS rag.ix_indexing_jobs_instruction_version_id "
        "RENAME TO ix_indexing_jobs_document_version_id"
    )
    op.execute(
        "ALTER INDEX IF EXISTS rag.ix_document_chunks_instruction_version_id "
        "RENAME TO ix_document_chunks_document_version_id"
    )
    op.execute(
        "ALTER INDEX IF EXISTS rag.ix_semantic_cache_sources_instruction_id "
        "RENAME TO ix_semantic_cache_sources_document_id"
    )
    op.execute(
        "ALTER INDEX IF EXISTS rag.ix_query_audit_citations_instruction_id "
        "RENAME TO ix_query_audit_citations_document_id"
    )
    _create_reporting_views()


def downgrade() -> None:
    op.execute("DROP VIEW IF EXISTS rag.v_feedback_summary")
    op.execute("DROP VIEW IF EXISTS rag.v_query_audit_with_citations")
    op.execute(
        """
        DO $$
        BEGIN
            IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'rag' AND table_name = 'query_audit_citations' AND column_name = 'document_version_id') THEN
                ALTER TABLE rag.query_audit_citations RENAME COLUMN document_version_id TO instruction_version_id;
            END IF;
            IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'rag' AND table_name = 'query_audit_citations' AND column_name = 'document_id') THEN
                ALTER TABLE rag.query_audit_citations RENAME COLUMN document_id TO instruction_id;
            END IF;
            IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'rag' AND table_name = 'semantic_cache_sources' AND column_name = 'document_version_id') THEN
                ALTER TABLE rag.semantic_cache_sources RENAME COLUMN document_version_id TO instruction_version_id;
            END IF;
            IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'rag' AND table_name = 'semantic_cache_sources' AND column_name = 'document_id') THEN
                ALTER TABLE rag.semantic_cache_sources RENAME COLUMN document_id TO instruction_id;
            END IF;
            IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'rag' AND table_name = 'document_chunks' AND column_name = 'document_version_id') THEN
                ALTER TABLE rag.document_chunks RENAME COLUMN document_version_id TO instruction_version_id;
            END IF;
            IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'rag' AND table_name = 'document_chunks' AND column_name = 'document_id') THEN
                ALTER TABLE rag.document_chunks RENAME COLUMN document_id TO instruction_id;
            END IF;
            IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'rag' AND table_name = 'indexing_jobs' AND column_name = 'document_version_id') THEN
                ALTER TABLE rag.indexing_jobs RENAME COLUMN document_version_id TO instruction_version_id;
            END IF;
            IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'rag' AND table_name = 'indexing_jobs' AND column_name = 'document_id') THEN
                ALTER TABLE rag.indexing_jobs RENAME COLUMN document_id TO instruction_id;
            END IF;
        END $$;
        """
    )
    op.execute(
        "ALTER INDEX IF EXISTS rag.ix_query_audit_citations_document_id "
        "RENAME TO ix_query_audit_citations_instruction_id"
    )
    op.execute(
        "ALTER INDEX IF EXISTS rag.ix_semantic_cache_sources_document_id "
        "RENAME TO ix_semantic_cache_sources_instruction_id"
    )
    op.execute(
        "ALTER INDEX IF EXISTS rag.ix_document_chunks_document_version_id "
        "RENAME TO ix_document_chunks_instruction_version_id"
    )
    op.execute(
        "ALTER INDEX IF EXISTS rag.ix_indexing_jobs_document_version_id "
        "RENAME TO ix_indexing_jobs_instruction_version_id"
    )
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
            citation.instruction_id,
            citation.instruction_version_id,
            citation.heading_path,
            citation.created_at AS citation_created_at
        FROM rag.query_audit_events event
        LEFT JOIN rag.query_audit_citations citation
            ON citation.query_audit_event_id = event.id
        """
    )
    _create_feedback_summary_view()


def _create_reporting_views() -> None:
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
    _create_feedback_summary_view()


def _create_feedback_summary_view() -> None:
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
