"""v2: change embedding column to VECTOR(1024) for multilingual model

This migration is part of the v2 generic refactor. See docs/adr/0003-multilingual-embeddings.md
and docs/v2/03-phases.md (Phase 1.2).

The MVP used `text-embedding-3-small` at native 1536 dims. v2 standardises on
1024 dims so both `text-embedding-3-large` (with `dimensions=1024` truncation)
and `BGE-M3` self-hosted via TEI fit the same column.

CRITICAL: applying this migration drops existing embedding values. A reindex
job MUST run before the system serves chat traffic. Old chunks are marked
`is_active=false` so retrieval returns no results until reindexed.
"""

from collections.abc import Sequence

from alembic import op

revision: str = "20260522_120000"
down_revision: str | None = "20260520_180000"
branch_labels: str | Sequence[str] | None = None
depends_on: str | Sequence[str] | None = None


def upgrade() -> None:
    # Mark all chunks inactive so chat returns no stale 1536-d embeddings while a
    # reindex job rewrites them at 1024 dims.
    op.execute(
        """
        UPDATE rag.document_chunks SET is_active = false WHERE is_active = true;
        """
    )

    # Drop the HNSW index because pgvector cannot ALTER a vector column's dimension
    # while an index references it.
    op.execute(
        """
        DROP INDEX IF EXISTS rag.ix_document_chunks_embedding_hnsw;
        """
    )

    # Historical chunks must remain for query-audit citation references, but their
    # 1536-d embeddings are no longer valid. Make the column nullable so inactive
    # historical chunks can keep their rows while reindexing writes fresh 1024-d
    # embeddings for active chunks.
    op.execute(
        """
        ALTER TABLE rag.document_chunks
            ALTER COLUMN embedding DROP NOT NULL,
            ALTER COLUMN embedding TYPE vector(1024) USING NULL;
        """
    )

    # Recreate HNSW with the same parameters at the new dimension.
    op.execute(
        """
        CREATE INDEX ix_document_chunks_embedding_hnsw
            ON rag.document_chunks
            USING hnsw (embedding vector_cosine_ops)
            WITH (m = 16, ef_construction = 64);
        """
    )

    # Cache entries become invalid because the cached question_embedding column also
    # uses the 1536-d vector type. Clear the cache.
    op.execute(
        """
        DELETE FROM rag.semantic_cache_entries;
        """
    )
    op.execute(
        """
        ALTER TABLE rag.semantic_cache_entries
            ALTER COLUMN question_embedding TYPE vector(1024) USING NULL;
        """
    )


def downgrade() -> None:
    op.execute(
        """
        DROP INDEX IF EXISTS rag.ix_document_chunks_embedding_hnsw;
        """
    )
    op.execute(
        """
        ALTER TABLE rag.document_chunks
            ALTER COLUMN embedding DROP NOT NULL,
            ALTER COLUMN embedding TYPE vector(1536) USING NULL;
        """
    )
    op.execute(
        """
        CREATE INDEX ix_document_chunks_embedding_hnsw
            ON rag.document_chunks
            USING hnsw (embedding vector_cosine_ops)
            WITH (m = 16, ef_construction = 64);
        """
    )
    op.execute(
        """
        DELETE FROM rag.semantic_cache_entries;
        """
    )
    op.execute(
        """
        ALTER TABLE rag.semantic_cache_entries
            ALTER COLUMN question_embedding TYPE vector(1536) USING NULL;
        """
    )
