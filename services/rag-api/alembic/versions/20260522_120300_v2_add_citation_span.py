"""v2: add citation span (text_quote, page_number) on query_audit_citations

This migration is part of the v2 generic refactor. See docs/v2/03-phases.md (Phase 5.4).

The MVP citations identify the source chunk but do not record the exact substring the
LLM cited. v2 stores that substring (`text_quote`) so the docs viewer can highlight it.
For PDF-imported documents, `page_number` records the original page so the viewer can
deep-link to it.
"""

from collections.abc import Sequence

from alembic import op

revision: str = "20260522_120300"
down_revision: str | None = "20260522_120200"
branch_labels: str | Sequence[str] | None = None
depends_on: str | Sequence[str] | None = None


def upgrade() -> None:
    op.execute(
        """
        ALTER TABLE rag.query_audit_citations
            ADD COLUMN text_quote text,
            ADD COLUMN page_number int;
        """
    )


def downgrade() -> None:
    op.execute(
        """
        ALTER TABLE rag.query_audit_citations
            DROP COLUMN IF EXISTS text_quote,
            DROP COLUMN IF EXISTS page_number;
        """
    )
