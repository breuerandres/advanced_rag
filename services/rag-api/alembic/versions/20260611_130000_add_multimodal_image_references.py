"""add multimodal image references"""

from alembic import op
import sqlalchemy as sa


revision = "20260611_130000"
down_revision = "20260610_120000"
branch_labels = None
depends_on = None


def upgrade() -> None:
    op.create_table(
        "document_chunk_images",
        sa.Column("id", sa.Uuid(), primary_key=True),
        sa.Column("chunk_id", sa.Uuid(), nullable=False),
        sa.Column("document_id", sa.Uuid(), nullable=False),
        sa.Column("document_version_id", sa.Uuid(), nullable=False),
        sa.Column("image_id", sa.Uuid(), nullable=False),
        sa.Column("ordinal", sa.Integer(), nullable=False),
        sa.Column("alt_text", sa.Text(), nullable=True),
        sa.Column("caption", sa.Text(), nullable=True),
        sa.Column(
            "created_at",
            sa.DateTime(timezone=True),
            nullable=False,
            server_default=sa.text("now()"),
        ),
        sa.UniqueConstraint("chunk_id", "image_id", name="uq_document_chunk_images_chunk_image"),
        schema="rag",
    )
    op.create_index(
        "ix_document_chunk_images_chunk_id",
        "document_chunk_images",
        ["chunk_id"],
        schema="rag",
    )
    op.create_index(
        "ix_document_chunk_images_document_version_id",
        "document_chunk_images",
        ["document_version_id"],
        schema="rag",
    )
    op.add_column(
        "query_audit_events",
        sa.Column("multimodal_used", sa.Boolean(), nullable=False, server_default=sa.text("false")),
        schema="rag",
    )
    op.add_column(
        "query_audit_events",
        sa.Column("multimodal_image_count", sa.Integer(), nullable=False, server_default=sa.text("0")),
        schema="rag",
    )
    op.add_column(
        "query_audit_events",
        sa.Column("multimodal_image_detail", sa.Text(), nullable=True),
        schema="rag",
    )
    op.add_column(
        "query_audit_events",
        sa.Column(
            "multimodal_image_bytes_total",
            sa.Integer(),
            nullable=False,
            server_default=sa.text("0"),
        ),
        schema="rag",
    )
    op.add_column(
        "query_audit_events",
        sa.Column("multimodal_image_ids", sa.JSON(), nullable=True),
        schema="rag",
    )


def downgrade() -> None:
    op.drop_column("query_audit_events", "multimodal_image_ids", schema="rag")
    op.drop_column("query_audit_events", "multimodal_image_bytes_total", schema="rag")
    op.drop_column("query_audit_events", "multimodal_image_detail", schema="rag")
    op.drop_column("query_audit_events", "multimodal_image_count", schema="rag")
    op.drop_column("query_audit_events", "multimodal_used", schema="rag")
    op.drop_index(
        "ix_document_chunk_images_document_version_id",
        table_name="document_chunk_images",
        schema="rag",
    )
    op.drop_index(
        "ix_document_chunk_images_chunk_id",
        table_name="document_chunk_images",
        schema="rag",
    )
    op.drop_table("document_chunk_images", schema="rag")
