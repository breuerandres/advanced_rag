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
    vector_top_k: int = 20
    bm25_top_k: int = 20
    rrf_k: int = 60
    final_top_k: int = 30  # before reranker reduction


# Branch-aware document access predicate. Applied INSIDE each candidate CTE so rows
# outside the user's scope never enter the top-K. Semantics mirror the .NET
# `DocumentAccessPolicy`:
#   * A global admin bypasses the filter entirely.
#   * A document is visible when ANY of its access rules matches (OR between rules).
#   * Within one rule the organizational-unit condition AND the group condition must
#     both hold (each condition is trivially satisfied when that dimension is absent).
#   * The organizational-unit condition matches the company-wide root rule, or when the
#     rule's unit is an ancestor/descendant of the user's unit (closure join).
#   * A rule with neither an organizational unit nor any group is invalid and is
#     ignored defensively here, mirroring the application-layer rejection.
_ACCESS_RULE_PREDICATE = """
      AND (
          CAST(:is_global_admin AS boolean)
          OR EXISTS (
              SELECT 1
              FROM app.document_permissions p
              WHERE p.document_id = chunk.document_id
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
"""


# The SQL is laid out as a single statement with two CTE candidates and one UNION /
# aggregate that fuses them. We use named bindings throughout. The `unaccent` call on
# the question goes through the IMMUTABLE wrapper added in the v2 BM25 migration.
HYBRID_RETRIEVAL_SQL = text(
    f"""
WITH vector_candidates AS (
    SELECT
        chunk.id,
        chunk.document_id,
        chunk.document_version_id,
        chunk.content,
        chunk.heading_path,
        row_number() OVER (ORDER BY chunk.embedding <=> CAST(:q_embedding AS vector)) AS rank
    FROM rag.document_chunks chunk
    WHERE chunk.corpus = :corpus
      AND chunk.is_active = true
      {_ACCESS_RULE_PREDICATE}
      AND (
          CAST(:dimension_value_filter AS uuid[]) IS NULL
          OR EXISTS (
              SELECT 1 FROM app.document_dimension_values ddv
              WHERE ddv.document_id = chunk.document_id
                AND ddv.dimension_value_id = ANY(CAST(:dimension_value_filter AS uuid[]))
          )
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
    WHERE chunk.corpus = :corpus
      AND chunk.is_active = true
      AND (
          chunk.content_tsv @@ websearch_to_tsquery('simple', rag.f_immutable_unaccent(:q_text))
          OR chunk.content % :q_text
      )
      {_ACCESS_RULE_PREDICATE}
      AND (
          CAST(:dimension_value_filter AS uuid[]) IS NULL
          OR EXISTS (
              SELECT 1 FROM app.document_dimension_values ddv
              WHERE ddv.document_id = chunk.document_id
                AND ddv.dimension_value_id = ANY(CAST(:dimension_value_filter AS uuid[]))
          )
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
