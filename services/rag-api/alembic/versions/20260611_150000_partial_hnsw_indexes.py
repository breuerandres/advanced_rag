"""partial per-corpus hnsw indexes

Replace the single global HNSW index over `rag.document_chunks.embedding` with two
partial indexes, one per corpus, that exclude inactive chunks. This keeps each index
smaller (less HNSW bloat) and lets the planner prove the partial predicate when the
corpus is inlined as a literal in the retrieval SQL. Deactivated chunks drop out of the
index automatically on vacuum.
"""

from alembic import op


revision = "20260611_150000"
down_revision = "20260611_130000"
branch_labels = None
depends_on = None


def upgrade() -> None:
    op.execute("DROP INDEX IF EXISTS rag.ix_document_chunks_embedding_hnsw;")
    op.execute(
        """
        CREATE INDEX ix_document_chunks_embedding_hnsw_published
            ON rag.document_chunks
            USING hnsw (embedding vector_cosine_ops)
            WITH (m = 16, ef_construction = 64)
            WHERE corpus = 'published' AND is_active = true;
        """
    )
    op.execute(
        """
        CREATE INDEX ix_document_chunks_embedding_hnsw_preview
            ON rag.document_chunks
            USING hnsw (embedding vector_cosine_ops)
            WITH (m = 16, ef_construction = 64)
            WHERE corpus = 'preview' AND is_active = true;
        """
    )


def downgrade() -> None:
    op.execute("DROP INDEX IF EXISTS rag.ix_document_chunks_embedding_hnsw_published;")
    op.execute("DROP INDEX IF EXISTS rag.ix_document_chunks_embedding_hnsw_preview;")
    op.execute(
        """
        CREATE INDEX ix_document_chunks_embedding_hnsw
            ON rag.document_chunks
            USING hnsw (embedding vector_cosine_ops)
            WITH (m = 16, ef_construction = 64);
        """
    )
