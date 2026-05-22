"""v2: add BM25 (tsvector) and trigram columns on document_chunks

This migration is part of the v2 generic refactor. See docs/adr/0004-postgres-bm25.md
and docs/v2/03-phases.md (Phase 2.3).

Adds:
- `content_tsv` generated column: language-agnostic ts_vector (configuration 'simple'
  + unaccent) used by the hybrid retrieval BM25-side candidate query.
- `language` text column to record the source document language at chunk level.
- GIN index on `content_tsv` for fast BM25-style ranking via `ts_rank_cd`.
- GIN index on `content gin_trgm_ops` for trigram fuzzy matching of codes/identifiers.

Requires the `unaccent` and `pg_trgm` extensions which are installed by postgres-init.
"""

from collections.abc import Sequence

from alembic import op

revision: str = "20260522_120100"
down_revision: str | None = "20260522_120000"
branch_labels: str | Sequence[str] | None = None
depends_on: str | Sequence[str] | None = None


def upgrade() -> None:
    # unaccent must be IMMUTABLE for a generated column. The default install marks it
    # STABLE; wrap with a custom IMMUTABLE wrapper or rely on the project's existing
    # IMMUTABLE wrapper if present. Falls back to lowercasing content for the tsvector.
    op.execute(
        """
        CREATE OR REPLACE FUNCTION rag.f_immutable_unaccent(text)
            RETURNS text AS $$
                SELECT public.unaccent('public.unaccent', $1);
            $$ LANGUAGE sql IMMUTABLE PARALLEL SAFE STRICT;
        """
    )

    op.execute(
        """
        ALTER TABLE rag.document_chunks
            ADD COLUMN content_tsv tsvector
                GENERATED ALWAYS AS (
                    to_tsvector('simple', rag.f_immutable_unaccent(coalesce(content, '')))
                ) STORED,
            ADD COLUMN language text;
        """
    )

    op.execute(
        """
        CREATE INDEX ix_document_chunks_content_tsv
            ON rag.document_chunks USING GIN (content_tsv);
        """
    )

    op.execute(
        """
        CREATE INDEX ix_document_chunks_content_trgm
            ON rag.document_chunks USING GIN (content gin_trgm_ops);
        """
    )

    op.execute(
        """
        CREATE INDEX ix_document_chunks_language
            ON rag.document_chunks (language);
        """
    )


def downgrade() -> None:
    op.execute("DROP INDEX IF EXISTS rag.ix_document_chunks_content_tsv;")
    op.execute("DROP INDEX IF EXISTS rag.ix_document_chunks_content_trgm;")
    op.execute("DROP INDEX IF EXISTS rag.ix_document_chunks_language;")
    op.execute(
        """
        ALTER TABLE rag.document_chunks
            DROP COLUMN IF EXISTS content_tsv,
            DROP COLUMN IF EXISTS language;
        """
    )
    op.execute("DROP FUNCTION IF EXISTS rag.f_immutable_unaccent(text);")
