"""Add scope_document_id to rag.query_audit_events.

Document-scoped mini-chat requests (docs-web) audit which document the retrieval
was restricted to. NULL means a normal corpus-wide chat request. See
docs/superpowers/specs/2026-06-10-docs-web-redesign-and-doc-chat-design.md.
"""

from collections.abc import Sequence

from alembic import op

revision: str = "20260610_120000"
down_revision: str | None = "20260522_134100"
branch_labels: str | Sequence[str] | None = None
depends_on: str | Sequence[str] | None = None


def upgrade() -> None:
    op.execute("ALTER TABLE rag.query_audit_events ADD COLUMN scope_document_id uuid;")


def downgrade() -> None:
    op.execute("ALTER TABLE rag.query_audit_events DROP COLUMN IF EXISTS scope_document_id;")
