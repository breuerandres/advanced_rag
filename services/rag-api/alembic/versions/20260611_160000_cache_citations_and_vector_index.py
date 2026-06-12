"""semantic cache citations column and question-embedding vector index

Store the original citations alongside each cached answer so cache hits replay the exact
citations of the answer that was cached (instead of reconstructing "first chunk of source
document"), and add an HNSW index on `question_embedding` so cache lookups are a pgvector
nearest-neighbor query instead of a Python full-scan per partition.
"""

from alembic import op


revision = "20260611_160000"
down_revision = "20260611_150000"
branch_labels = None
depends_on = None


def upgrade() -> None:
    op.execute(
        """
        ALTER TABLE rag.semantic_cache_entries
            ADD COLUMN citations jsonb NOT NULL DEFAULT '[]'::jsonb;
        """
    )
    op.execute(
        """
        CREATE INDEX ix_semantic_cache_entries_question_embedding_hnsw
            ON rag.semantic_cache_entries
            USING hnsw (question_embedding vector_cosine_ops)
            WITH (m = 16, ef_construction = 64);
        """
    )
    # Pre-existing entries have no stored citations; they cannot be replayed
    # faithfully, so retire them instead of serving degraded hits.
    op.execute("DELETE FROM rag.semantic_cache_entries WHERE citations = '[]'::jsonb;")


def downgrade() -> None:
    op.execute("DROP INDEX IF EXISTS rag.ix_semantic_cache_entries_question_embedding_hnsw;")
    op.execute("ALTER TABLE rag.semantic_cache_entries DROP COLUMN IF EXISTS citations;")
