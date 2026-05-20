"""grant reporting views after documents rename"""

from collections.abc import Sequence

from alembic import op

revision: str = "20260520_180000"
down_revision: str | None = "20260520_170000"
branch_labels: str | Sequence[str] | None = None
depends_on: str | Sequence[str] | None = None


def upgrade() -> None:
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
    op.execute(
        """
        DO $$
        BEGIN
            IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_reporting_reader') THEN
                REVOKE SELECT ON rag.v_feedback_summary FROM app_reporting_reader;
                REVOKE SELECT ON rag.v_query_audit_with_citations FROM app_reporting_reader;
            END IF;
        END
        $$;
        """
    )
