"""Reranking step: cross-encoder over hybrid retrieval candidates.

See docs/adr/0002-hybrid-retrieval.md.

The reranker is optional. If `enable_reranker` is false in tenant_config or a per-query
flag disables it, the input list is truncated to `final_top_k` and returned unchanged.
"""

from __future__ import annotations

from advanced_rag.providers.base import IRerankerProvider
from advanced_rag.rag.hybrid_retrieval import HybridCandidate


async def rerank_candidates(
    *,
    reranker: IRerankerProvider | None,
    query: str,
    candidates: list[HybridCandidate],
    top_k: int,
) -> tuple[list[HybridCandidate], dict | None]:
    """Rerank candidates with a cross-encoder; fall back to truncation if no reranker.

    Returns the top-`top_k` candidates and an optional dict with audit info:
    `{"reranker_model": str, "top_score": float}`. Returns `None` for audit when the
    reranker is disabled.
    """
    if reranker is None or not candidates:
        return candidates[:top_k], None

    documents = [candidate.content for candidate in candidates]
    results = await reranker.rerank(query=query, documents=documents, top_k=top_k)

    if not results:
        return candidates[:top_k], None

    reranked = [candidates[r.original_index] for r in results]
    audit = {
        "reranker_model": reranker.model,
        "top_score": float(results[0].score),
    }
    return reranked, audit
