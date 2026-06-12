"""Hybrid retrieval: vector kNN + BM25 (tsvector) fused with Reciprocal Rank Fusion.

See docs/adr/0002-hybrid-retrieval.md, docs/adr/0004-postgres-bm25.md.

The retrieval SQL runs both retrievers in a single statement (CTE) so Postgres can use
its query planner across both indexes (HNSW for vector, GIN for tsvector). RRF score is
computed in SQL.

Permission and dimension filters are applied INSIDE each CTE so candidates beyond the
user's scope never enter the top-K. The hybrid result is fed to a cross-encoder
reranker (`rag/rerank.py`) for the final top-K.
"""

from __future__ import annotations

from typing import Any
from uuid import UUID

from pydantic import BaseModel, ConfigDict
from sqlalchemy import text
from sqlalchemy.ext.asyncio import AsyncConnection


async def detect_iterative_scan_support(connection: AsyncConnection) -> bool:
    """pgvector >= 0.8.0 supports `SET hnsw.iterative_scan`.

    Probed once at startup; older images silently fall back to `ef_search` only.
    """
    result = await connection.execute(
        text("select extversion from pg_extension where extname = 'vector'")
    )
    version = result.scalar_one_or_none()
    if not version:
        return False
    parts = str(version).split(".")
    try:
        major, minor = int(parts[0]), int(parts[1])
    except (IndexError, ValueError):
        return False
    return (major, minor) >= (0, 8)


class HybridCandidate(BaseModel):
    """One retrieved chunk before reranking."""

    model_config = ConfigDict(frozen=True)

    chunk_id: UUID
    document_id: UUID
    document_version_id: UUID
    content: str
    heading_path: list[str]
    rrf_score: float


class HybridRetrievalParams(BaseModel):
    model_config = ConfigDict(frozen=True)

    corpus: str
    user_groups: list[UUID]
    user_organizational_unit_id: UUID
    root_organizational_unit_id: UUID
    is_global_admin: bool = False
    dimension_value_filter: list[UUID] | None = None
    scope_document_id: UUID | None = None
    vector_top_k: int = 20
    bm25_top_k: int = 20
    rrf_k: int = 60
    final_top_k: int = 30  # before reranker reduction


# Permission resolution + lifecycle + scope + dimension filtering happen ONCE per query
# in a MATERIALIZED `allowed_documents` CTE over `app.documents`. Both candidate CTEs
# semi-join it, so the expensive correlated permission predicate is evaluated per
# document, not per candidate chunk row.
#
# Access semantics mirror the .NET `DocumentAccessPolicy`:
#   * A global admin bypasses the access-rule filter entirely (not the lifecycle state).
#   * A document is visible when ANY of its access rules matches (OR between rules).
#   * Within one rule the organizational-unit condition AND the group condition must
#     both hold (each condition is trivially satisfied when that dimension is absent).
#   * The organizational-unit condition matches the company-wide root rule, or when the
#     rule's unit is an ancestor/descendant of the user's unit (closure join).
#   * A rule with neither an organizational unit nor any group is invalid and is
#     ignored defensively here, mirroring the application-layer rejection.
#
# Lifecycle: for the published corpus the chunk must belong to the document's CURRENT
# published version (self-healing against missed deactivations); for the preview corpus
# any non-archived document qualifies (version hygiene there is handled by `is_active`).
# The lifecycle comparison lives in the candidate join condition because it depends on
# the chunk's `document_version_id`; the CTE carries the pointers needed to evaluate it.
#
# The `unaccent` call on the question goes through the IMMUTABLE wrapper added in the v2
# BM25 migration.
HYBRID_RETRIEVAL_SQL = text(
    """
WITH allowed_documents AS MATERIALIZED (
    SELECT doc."Id" AS document_id,
           doc.current_published_version_id,
           doc.current_draft_version_id,
           doc.current_state
    FROM app.documents doc
    WHERE
      (
          CAST(:is_global_admin AS boolean)
          OR EXISTS (
              SELECT 1
              FROM app.document_permissions p
              WHERE p.document_id = doc."Id"
                AND (
                    p.organizational_unit_id IS NOT NULL
                    OR EXISTS (
                        SELECT 1
                        FROM app.document_permission_groups pg
                        WHERE pg.document_permission_id = p."Id"
                    )
                )
                AND (
                    p.organizational_unit_id IS NULL
                    OR p.organizational_unit_id = :root_organizational_unit_id
                    OR EXISTS (
                        SELECT 1
                        FROM app.organizational_unit_closure c
                        WHERE
                            (c.ancestor_id = p.organizational_unit_id
                                AND c.descendant_id = :user_organizational_unit_id)
                            OR
                            (c.ancestor_id = :user_organizational_unit_id
                                AND c.descendant_id = p.organizational_unit_id)
                    )
                )
                AND (
                    NOT EXISTS (
                        SELECT 1
                        FROM app.document_permission_groups pg
                        WHERE pg.document_permission_id = p."Id"
                    )
                    OR EXISTS (
                        SELECT 1
                        FROM app.document_permission_groups pg
                        WHERE pg.document_permission_id = p."Id"
                          AND pg.group_id = ANY(CAST(:user_groups AS uuid[]))
                    )
                )
          )
      )
      AND (
          CAST(:scope_document_id AS uuid) IS NULL
          OR doc."Id" = CAST(:scope_document_id AS uuid)
      )
      AND (
          CAST(:dimension_value_filter AS uuid[]) IS NULL
          OR EXISTS (
              SELECT 1 FROM app.document_dimension_values ddv
              WHERE ddv.document_id = doc."Id"
                AND ddv.dimension_value_id = ANY(CAST(:dimension_value_filter AS uuid[]))
          )
      )
),
vector_candidates AS (
    SELECT
        chunk.id,
        chunk.document_id,
        chunk.document_version_id,
        chunk.content,
        chunk.heading_path,
        row_number() OVER (ORDER BY chunk.embedding <=> CAST(:q_embedding AS vector)) AS rank
    FROM rag.document_chunks chunk
    JOIN allowed_documents ad ON ad.document_id = chunk.document_id
    WHERE chunk.corpus = :corpus
      AND chunk.is_active = true
      AND (
          (chunk.corpus = 'published'
              AND ad.current_state = 'Published'
              AND ad.current_published_version_id = chunk.document_version_id)
          OR
          (chunk.corpus = 'preview' AND ad.current_state <> 'Archived')
      )
    ORDER BY chunk.embedding <=> CAST(:q_embedding AS vector)
    LIMIT :vector_top_k
),
bm25_candidates AS (
    SELECT
        chunk.id,
        chunk.document_id,
        chunk.document_version_id,
        chunk.content,
        chunk.heading_path,
        row_number() OVER (
            ORDER BY
                ts_rank_cd(chunk.content_tsv, websearch_to_tsquery('simple', rag.f_immutable_unaccent(:q_text))) DESC,
                similarity(chunk.content, :q_text) DESC
        ) AS rank
    FROM rag.document_chunks chunk
    JOIN allowed_documents ad ON ad.document_id = chunk.document_id
    WHERE chunk.corpus = :corpus
      AND chunk.is_active = true
      AND (
          (chunk.corpus = 'published'
              AND ad.current_state = 'Published'
              AND ad.current_published_version_id = chunk.document_version_id)
          OR
          (chunk.corpus = 'preview' AND ad.current_state <> 'Archived')
      )
      AND (
          chunk.content_tsv @@ websearch_to_tsquery('simple', rag.f_immutable_unaccent(:q_text))
          OR chunk.content % :q_text
      )
    ORDER BY
        ts_rank_cd(chunk.content_tsv, websearch_to_tsquery('simple', rag.f_immutable_unaccent(:q_text))) DESC,
        similarity(chunk.content, :q_text) DESC
    LIMIT :bm25_top_k
),
fused AS (
    SELECT
        id,
        document_id,
        document_version_id,
        content,
        heading_path,
        SUM(1.0 / (CAST(:rrf_k AS float) + rank)) AS rrf_score
    FROM (
        SELECT id, document_id, document_version_id, content, heading_path, rank FROM vector_candidates
        UNION ALL
        SELECT id, document_id, document_version_id, content, heading_path, rank FROM bm25_candidates
    ) AS combined
    GROUP BY id, document_id, document_version_id, content, heading_path
)
SELECT id, document_id, document_version_id, content, heading_path, rrf_score
FROM fused
ORDER BY rrf_score DESC
LIMIT :final_top_k
"""
)


async def hybrid_retrieve(
    connection: AsyncConnection,
    *,
    q_text: str,
    q_embedding: list[float],
    params: HybridRetrievalParams,
) -> list[HybridCandidate]:
    """Run the hybrid retrieval SQL and return ordered candidates.

    The caller is responsible for embedding `q_text` and passing it as `q_embedding`.
    Same query goes to BM25 via `q_text`. For multi-turn / rewritten questions, the
    caller should pass the rewritten question as both `q_text` and the embedding source.
    """
    result = await connection.execute(
        HYBRID_RETRIEVAL_SQL,
        {
            "q_text": q_text,
            "q_embedding": _vector_literal(q_embedding),
            "corpus": params.corpus,
            "is_global_admin": params.is_global_admin,
            "user_groups": [str(group_id) for group_id in params.user_groups],
            "user_organizational_unit_id": params.user_organizational_unit_id,
            "root_organizational_unit_id": params.root_organizational_unit_id,
            "dimension_value_filter": (
                [str(v) for v in params.dimension_value_filter]
                if params.dimension_value_filter is not None
                else None
            ),
            "scope_document_id": (
                str(params.scope_document_id) if params.scope_document_id is not None else None
            ),
            "vector_top_k": params.vector_top_k,
            "bm25_top_k": params.bm25_top_k,
            "rrf_k": params.rrf_k,
            "final_top_k": params.final_top_k,
        },
    )
    rows = result.fetchall()
    return [
        HybridCandidate(
            chunk_id=row.id,
            document_id=row.document_id,
            document_version_id=row.document_version_id,
            content=row.content,
            heading_path=list(row.heading_path or []),
            rrf_score=float(row.rrf_score),
        )
        for row in rows
    ]


def _vector_literal(vec: list[float]) -> Any:
    """Convert a Python list to a pgvector string literal compatible with `vector` cast.

    SQLAlchemy 2 + asyncpg accept a Python list bound to a `vector` parameter, but only
    if the column type is registered. To keep this module portable across project setups
    (some configure pgvector.asyncpg adapters, some don't), we serialise to a string
    representation that the explicit `CAST(:q_embedding AS vector)` in SQL accepts.
    """
    return "[" + ",".join(repr(float(x)) for x in vec) + "]"
