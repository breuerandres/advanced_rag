"""create initial rag schema"""

from collections.abc import Sequence

import sqlalchemy as sa
from alembic import op
from pgvector.sqlalchemy import Vector

revision: str = "20260513_184500"
down_revision: str | None = None
branch_labels: str | Sequence[str] | None = None
depends_on: str | Sequence[str] | None = None


def upgrade() -> None:
    op.create_table(
        "model_pricing",
        sa.Column("id", sa.Uuid(), primary_key=True),
        sa.Column("model_id", sa.Text(), nullable=False),
        sa.Column("model_kind", sa.Text(), nullable=False),
        sa.Column("input_token_price_usd", sa.Numeric(18, 12), nullable=False),
        sa.Column("cached_token_price_usd", sa.Numeric(18, 12), nullable=True),
        sa.Column("output_token_price_usd", sa.Numeric(18, 12), nullable=True),
        sa.Column("effective_from", sa.DateTime(timezone=True), nullable=False),
        sa.Column("effective_to", sa.DateTime(timezone=True), nullable=True),
        sa.Column(
            "created_at",
            sa.DateTime(timezone=True),
            nullable=False,
            server_default=sa.text("now()"),
        ),
        sa.CheckConstraint(
            "model_kind in ('chat', 'embedding')",
            name="ck_model_pricing_model_kind",
        ),
        schema="rag",
    )
    op.create_index(
        "ix_model_pricing_model_effective",
        "model_pricing",
        ["model_id", "effective_from", "effective_to"],
        schema="rag",
    )

    op.create_table(
        "indexing_jobs",
        sa.Column("id", sa.Uuid(), primary_key=True),
        sa.Column("document_id", sa.Uuid(), nullable=False),
        sa.Column("document_version_id", sa.Uuid(), nullable=False),
        sa.Column("corpus", sa.Text(), nullable=False),
        sa.Column("status", sa.Text(), nullable=False),
        sa.Column("attempts", sa.Integer(), nullable=False, server_default="0"),
        sa.Column("chunker_version", sa.Integer(), nullable=False),
        sa.Column("error_code", sa.Text(), nullable=True),
        sa.Column("error_message", sa.Text(), nullable=True),
        sa.Column("chunk_count", sa.Integer(), nullable=True),
        sa.Column("embedding_model", sa.Text(), nullable=True),
        sa.Column("embedding_dimensions", sa.Integer(), nullable=False),
        sa.Column("embedding_tokens", sa.Integer(), nullable=True),
        sa.Column(
            "created_at",
            sa.DateTime(timezone=True),
            nullable=False,
            server_default=sa.text("now()"),
        ),
        sa.Column("started_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column("completed_at", sa.DateTime(timezone=True), nullable=True),
        sa.CheckConstraint(
            "corpus in ('published', 'preview')",
            name="ck_indexing_jobs_corpus",
        ),
        sa.CheckConstraint(
            "status in ('Pending', 'Running', 'Succeeded', 'Failed')",
            name="ck_indexing_jobs_status",
        ),
        schema="rag",
    )
    op.create_index(
        "ix_indexing_jobs_document_version_id",
        "indexing_jobs",
        ["document_version_id"],
        schema="rag",
    )

    op.create_table(
        "document_chunks",
        sa.Column("id", sa.Uuid(), primary_key=True),
        sa.Column("indexing_job_id", sa.Uuid(), nullable=False),
        sa.Column("document_id", sa.Uuid(), nullable=False),
        sa.Column("document_version_id", sa.Uuid(), nullable=False),
        sa.Column("corpus", sa.Text(), nullable=False),
        sa.Column("chunk_index", sa.Integer(), nullable=False),
        sa.Column("heading_path", sa.ARRAY(sa.Text()), nullable=False),
        sa.Column("token_count", sa.Integer(), nullable=False),
        sa.Column("char_count", sa.Integer(), nullable=False),
        sa.Column("content", sa.Text(), nullable=False),
        sa.Column("content_html", sa.Text(), nullable=False),
        sa.Column("embedding", Vector(1536), nullable=False),
        sa.Column("embedding_model", sa.Text(), nullable=False),
        sa.Column("is_active", sa.Boolean(), nullable=False, server_default="true"),
        sa.Column(
            "created_at",
            sa.DateTime(timezone=True),
            nullable=False,
            server_default=sa.text("now()"),
        ),
        sa.ForeignKeyConstraint(
            ["indexing_job_id"],
            ["rag.indexing_jobs.id"],
            ondelete="CASCADE",
        ),
        sa.UniqueConstraint(
            "document_version_id",
            "chunk_index",
            name="uq_document_chunks_version_chunk",
        ),
        sa.CheckConstraint(
            "corpus in ('published', 'preview')",
            name="ck_document_chunks_corpus",
        ),
        schema="rag",
    )
    op.create_index(
        "ix_document_chunks_document_version_id",
        "document_chunks",
        ["document_version_id"],
        schema="rag",
    )
    op.create_index(
        "ix_document_chunks_corpus_is_active",
        "document_chunks",
        ["corpus", "is_active"],
        schema="rag",
    )
    op.execute(
        "CREATE INDEX ix_document_chunks_embedding_hnsw "
        "ON rag.document_chunks USING hnsw (embedding vector_cosine_ops) "
        "WITH (m = 16, ef_construction = 64)"
    )

    op.create_table(
        "semantic_cache_entries",
        sa.Column("id", sa.Uuid(), primary_key=True),
        sa.Column("corpus", sa.Text(), nullable=False),
        sa.Column("access_scope_hash", sa.Text(), nullable=False),
        sa.Column("question_hash", sa.Text(), nullable=False),
        sa.Column("question", sa.Text(), nullable=False),
        sa.Column("answer", sa.Text(), nullable=False),
        sa.Column("question_embedding", Vector(1536), nullable=False),
        sa.Column("embedding_model", sa.Text(), nullable=False),
        sa.Column("embedding_dimensions", sa.Integer(), nullable=False),
        sa.Column("similarity_threshold", sa.Numeric(5, 4), nullable=False),
        sa.Column("cached_at", sa.DateTime(timezone=True), nullable=False),
        sa.Column("expires_at", sa.DateTime(timezone=True), nullable=False),
        sa.Column("created_by_query_audit_event_id", sa.Uuid(), nullable=True),
        sa.CheckConstraint(
            "corpus in ('published', 'preview')",
            name="ck_semantic_cache_entries_corpus",
        ),
        schema="rag",
    )
    op.create_index(
        "ix_semantic_cache_entries_scope_lookup",
        "semantic_cache_entries",
        ["corpus", "access_scope_hash", "expires_at"],
        schema="rag",
    )

    op.create_table(
        "semantic_cache_sources",
        sa.Column("cache_entry_id", sa.Uuid(), nullable=False),
        sa.Column("document_id", sa.Uuid(), nullable=False),
        sa.Column("document_version_id", sa.Uuid(), nullable=False),
        sa.ForeignKeyConstraint(
            ["cache_entry_id"],
            ["rag.semantic_cache_entries.id"],
            ondelete="CASCADE",
        ),
        sa.PrimaryKeyConstraint(
            "cache_entry_id",
            "document_id",
            "document_version_id",
            name="pk_semantic_cache_sources",
        ),
        schema="rag",
    )
    op.create_index(
        "ix_semantic_cache_sources_document_id",
        "semantic_cache_sources",
        ["document_id"],
        schema="rag",
    )

    op.create_table(
        "query_audit_events",
        sa.Column("id", sa.Uuid(), primary_key=True),
        sa.Column("user_id", sa.Uuid(), nullable=False),
        sa.Column("request_id", sa.Text(), nullable=False),
        sa.Column(
            "created_at",
            sa.DateTime(timezone=True),
            nullable=False,
            server_default=sa.text("now()"),
        ),
        sa.Column("question", sa.Text(), nullable=False),
        sa.Column("answer", sa.Text(), nullable=False),
        sa.Column("cache_hit", sa.Boolean(), nullable=False, server_default="false"),
        sa.Column("cached_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column("chat_model", sa.Text(), nullable=True),
        sa.Column("embedding_model", sa.Text(), nullable=False),
        sa.Column("embedding_dimensions", sa.Integer(), nullable=False),
        sa.Column("input_tokens", sa.Integer(), nullable=False, server_default="0"),
        sa.Column("cached_tokens", sa.Integer(), nullable=False, server_default="0"),
        sa.Column("output_tokens", sa.Integer(), nullable=False, server_default="0"),
        sa.Column("pricing_snapshot_id", sa.Uuid(), nullable=True),
        sa.Column("estimated_cost_usd", sa.Numeric(18, 8), nullable=False),
        sa.Column("latency_ms", sa.Integer(), nullable=False),
        sa.Column("access_scope_hash", sa.Text(), nullable=False),
        sa.Column("corpus", sa.Text(), nullable=False),
        sa.Column("prompt_version", sa.Integer(), nullable=False),
        sa.Column("chunker_version", sa.Integer(), nullable=False),
        sa.Column("feedback_value", sa.Text(), nullable=True),
        sa.Column("feedback_comment", sa.Text(), nullable=True),
        sa.Column("feedback_updated_at", sa.DateTime(timezone=True), nullable=True),
        sa.ForeignKeyConstraint(
            ["pricing_snapshot_id"],
            ["rag.model_pricing.id"],
            ondelete="RESTRICT",
        ),
        sa.CheckConstraint(
            "corpus in ('published', 'preview')",
            name="ck_query_audit_events_corpus",
        ),
        sa.CheckConstraint(
            "feedback_value is null or feedback_value in ('up', 'down')",
            name="ck_query_audit_events_feedback_value",
        ),
        schema="rag",
    )
    op.create_index(
        "ix_query_audit_events_created_at",
        "query_audit_events",
        ["created_at"],
        schema="rag",
    )
    op.create_index(
        "ix_query_audit_events_user_created_at",
        "query_audit_events",
        ["user_id", "created_at"],
        schema="rag",
    )

    op.create_table(
        "query_audit_citations",
        sa.Column("id", sa.Uuid(), primary_key=True),
        sa.Column("query_audit_event_id", sa.Uuid(), nullable=False),
        sa.Column("chunk_id", sa.Uuid(), nullable=False),
        sa.Column("document_id", sa.Uuid(), nullable=False),
        sa.Column("document_version_id", sa.Uuid(), nullable=False),
        sa.Column("heading_path", sa.ARRAY(sa.Text()), nullable=False),
        sa.Column(
            "created_at",
            sa.DateTime(timezone=True),
            nullable=False,
            server_default=sa.text("now()"),
        ),
        sa.ForeignKeyConstraint(
            ["query_audit_event_id"],
            ["rag.query_audit_events.id"],
            ondelete="CASCADE",
        ),
        sa.ForeignKeyConstraint(
            ["chunk_id"],
            ["rag.document_chunks.id"],
            ondelete="RESTRICT",
        ),
        schema="rag",
    )
    op.create_index(
        "ix_query_audit_citations_document_id",
        "query_audit_citations",
        ["document_id"],
        schema="rag",
    )
    op.create_index(
        "ix_query_audit_citations_query_audit_event_id",
        "query_audit_citations",
        ["query_audit_event_id"],
        schema="rag",
    )


def downgrade() -> None:
    op.drop_table("query_audit_citations", schema="rag")
    op.drop_table("query_audit_events", schema="rag")
    op.drop_table("semantic_cache_sources", schema="rag")
    op.drop_table("semantic_cache_entries", schema="rag")
    op.drop_index("ix_document_chunks_embedding_hnsw", table_name="document_chunks", schema="rag")
    op.drop_table("document_chunks", schema="rag")
    op.drop_table("indexing_jobs", schema="rag")
    op.drop_table("model_pricing", schema="rag")
