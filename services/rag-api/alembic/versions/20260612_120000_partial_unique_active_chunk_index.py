"""partial unique index on active document chunks

The original `uq_document_chunks_version_chunk` constraint enforced uniqueness of
`(document_version_id, chunk_index)` across *every* row. But re-indexing retains the
prior run's chunks for query-audit / citation history (marked `is_active = false`)
instead of deleting them, so a second indexing pass over the same version collided
with those retained inactive rows and failed with a UniqueViolation. The same version
id is re-indexed in two normal flows: a publish retry, and editing a draft then
re-publishing (`UpdateDraftAsync` reuses the draft version id).

Scope the uniqueness to active chunks only, matching the `is_active` partial-index
pattern already used for the per-corpus HNSW indexes. Inactive historical chunks may
now share `(document_version_id, chunk_index)`; retrieval and citations already filter
on `is_active`, so they are unaffected. The in-request pipeline deactivates the prior
chunks (within the same transaction) before inserting the fresh ones, so at most one
active row per `(version, chunk_index)` ever exists.
"""

from alembic import op


revision = "20260612_120000"
down_revision = "20260611_160000"
branch_labels = None
depends_on = None


def upgrade() -> None:
    op.execute(
        "ALTER TABLE rag.document_chunks "
        "DROP CONSTRAINT IF EXISTS uq_document_chunks_version_chunk;"
    )
    op.execute(
        """
        CREATE UNIQUE INDEX uq_document_chunks_version_chunk_active
            ON rag.document_chunks (document_version_id, chunk_index)
            WHERE is_active = true;
        """
    )


def downgrade() -> None:
    op.execute("DROP INDEX IF EXISTS rag.uq_document_chunks_version_chunk_active;")
    # Best effort: restoring the global constraint requires that no two retained
    # inactive rows share (document_version_id, chunk_index). Databases that
    # re-indexed a version under the partial index will hold such duplicates and
    # must be cleaned up before this downgrade can apply.
    op.execute(
        "ALTER TABLE rag.document_chunks "
        "ADD CONSTRAINT uq_document_chunks_version_chunk "
        "UNIQUE (document_version_id, chunk_index);"
    )
