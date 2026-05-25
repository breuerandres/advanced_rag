"""seed default model pricing rows"""

from collections.abc import Sequence

from alembic import op

revision: str = "20260522_134100"
down_revision: str | None = "20260522_120400"
branch_labels: str | Sequence[str] | None = None
depends_on: str | Sequence[str] | None = None


def upgrade() -> None:
    op.execute(
        """
        INSERT INTO rag.model_pricing (
            id,
            model_id,
            model_kind,
            input_token_price_usd,
            cached_token_price_usd,
            output_token_price_usd,
            effective_from,
            effective_to
        )
        VALUES
            (
                '30000000-0000-0000-0000-000000000001',
                'gpt-4.1-nano',
                'chat',
                0.000000100000,
                0.000000025000,
                0.000000400000,
                '2026-05-22T00:00:00Z',
                NULL
            ),
            (
                '30000000-0000-0000-0000-000000000002',
                'text-embedding-3-small',
                'embedding',
                0.000000020000,
                NULL,
                NULL,
                '2026-05-22T00:00:00Z',
                NULL
            )
        ON CONFLICT (id) DO UPDATE SET
            model_id = EXCLUDED.model_id,
            model_kind = EXCLUDED.model_kind,
            input_token_price_usd = EXCLUDED.input_token_price_usd,
            cached_token_price_usd = EXCLUDED.cached_token_price_usd,
            output_token_price_usd = EXCLUDED.output_token_price_usd,
            effective_from = EXCLUDED.effective_from,
            effective_to = EXCLUDED.effective_to;
        """
    )


def downgrade() -> None:
    op.execute(
        """
        DELETE FROM rag.model_pricing
        WHERE id IN (
            '30000000-0000-0000-0000-000000000001',
            '30000000-0000-0000-0000-000000000002'
        );
        """
    )
