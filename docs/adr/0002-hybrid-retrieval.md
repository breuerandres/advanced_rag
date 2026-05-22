# ADR-0002 — Hybrid retrieval: vector + BM25 + RRF + cross-encoder reranker

**Status**: Accepted (v2 design phase, 2026-05-22)
**Phase**: 2
**Supersedes**: `context/rag-spec.md` §Retrieval (vector-only)

## Context

The MVP's vector-only retrieval works well for natural-language paraphrase but fails
predictably on:

- Exact code matches: `IMA001`, `ABR522`, error codes, version strings.
- Rare proper nouns that aren't in the embedding model's pre-training set.
- Numeric identifiers (`#1234`, `2024-Q3`).
- Customer-specific vocabulary the model has never seen.

Cross-encoder reranking is the highest-leverage improvement after hybrid retrieval per
the RAGAS benchmarks and recent papers (LlamaIndex hybrid retrieval evaluation 2024-2025;
Anthropic contextual retrieval blog Oct 2024).

## Decision

Replace the single `vector kNN k=8` with this pipeline:

1. Vector kNN, `k=20` (HNSW on pgvector, cosine).
2. BM25 lexical search, `k=20` (Postgres `tsvector` + `pg_trgm`, see ADR-0004).
3. Reciprocal Rank Fusion (RRF) with `k_constant=60`, returns top-30 candidates.
4. Cross-encoder reranker (BGE-reranker-v2-m3 by default, configurable), returns final
   top-8.

The top-K values are stored in `tenant_config` (`rag_top_k_vector`, `rag_top_k_bm25`,
`rag_top_k_final`) so admins can tune.

The reranker is on by default but per-query `?rerank=false` is honoured. Reranker provider
is pluggable (ADR-0001).

## Alternatives considered

### Hybrid without reranker (vector + BM25 + RRF only)
Pros: Lower latency (saves 50–150ms), simpler.
Cons: Loses ~10–15% nDCG@10 in our internal evals on corp docs; reranker pays for itself.

### Vector + reranker (no BM25)
Pros: Simpler than hybrid.
Cons: Doesn't solve exact-match problem for codes and identifiers. Reranker can't promote
what retrieval didn't recall.

### Hybrid with weighted score fusion (not RRF)
Pros: Allows tuning the contribution of each retriever.
Cons: Weight needs per-corpus calibration. RRF is calibration-free and competitive with
weighted methods per recent literature.

### Switch to Elasticsearch for BM25
Pros: Best-in-class BM25.
Cons: One more service to operate, sync overhead with Postgres, multiplies infra cost. The
Postgres `tsvector` is good enough for the docs sizes we expect (≤10K docs, ≤500K chunks).
ADR-0004 expands on this.

## Consequences

**Positive**
- +10 to +20% nDCG@8 expected on mixed natural + lexical queries.
- Customer codes / identifiers reliably retrievable.
- Tunable per tenant.

**Negative**
- Latency adds 80–200ms (reranker on CPU TEI, single batch).
- Two indexes to maintain (HNSW + GIN).
- Pipeline complexity for new contributors.

**Risks / mitigations**
- Reranker over-promotes lexical matches → mitigated by RRF before rerank (lexical wins
  don't crowd out semantic at the candidate stage).
- Postgres BM25 (`ts_rank_cd`) is not exact BM25 → if quality lags, swap to `paradedb`
  extension (real BM25 in Postgres). Tracked but not done in v1.

## References

- ADR-0001 (provider abstraction; reranker is one provider)
- ADR-0004 (BM25 in Postgres)
- `services/rag-api/src/advanced_rag/rag/hybrid_retrieval.py`
- Anthropic, "Introducing Contextual Retrieval" (Oct 2024)
- Cormack, Clarke, Büttcher, "Reciprocal Rank Fusion outperforms Condorcet and individual Rank Learning Methods" (2009)
