"""v2: add unresolved_questions clustering table

This migration is part of the v2 generic refactor. See docs/v2/03-phases.md (Phase 5.6).

`rag.unresolved_questions` collects queries that the system flagged as low-confidence
(no citations, negative feedback, low rerank score). A nightly clustering job groups
similar questions so administrators can see "we keep getting asked X but have no
article that answers it".
"""

from collections.abc import Sequence

from alembic import op

revision: str = "20260522_120400"
down_revision: str | None = "20260522_120300"
branch_labels: str | Sequence[str] | None = None
depends_on: str | Sequence[str] | None = None


def upgrade() -> None:
    op.execute(
        """
        CREATE TABLE rag.unresolved_questions (
            id                   uuid PRIMARY KEY DEFAULT gen_random_uuid(),
            question_text        text NOT NULL,
            question_embedding   vector(1024),
            query_audit_event_id uuid REFERENCES rag.query_audit_events(id),
            reason               text NOT NULL CHECK (reason IN ('no_citations', 'feedback_negative', 'low_confidence', 'low_rerank_score')),
            language             text,
            cluster_id           uuid,
            cluster_label        text,
            status               text NOT NULL DEFAULT 'open' CHECK (status IN ('open', 'addressed', 'wont_fix')),
            created_at           timestamptz NOT NULL DEFAULT now(),
            updated_at           timestamptz NOT NULL DEFAULT now()
        );
        """
    )

    op.execute(
        """
        CREATE INDEX ix_unresolved_questions_cluster
            ON rag.unresolved_questions (cluster_id, status)
            WHERE cluster_id IS NOT NULL;
        """
    )

    op.execute(
        """
        CREATE INDEX ix_unresolved_questions_status_created
            ON rag.unresolved_questions (status, created_at DESC);
        """
    )

    # HNSW index so the clustering job can find nearest neighbours quickly.
    op.execute(
        """
        CREATE INDEX ix_unresolved_questions_embedding_hnsw
            ON rag.unresolved_questions
            USING hnsw (question_embedding vector_cosine_ops)
            WITH (m = 16, ef_construction = 64)
            WHERE question_embedding IS NOT NULL;
        """
    )


def downgrade() -> None:
    op.execute("DROP TABLE IF EXISTS rag.unresolved_questions;")
