# ADR-0003 — Multilingual embedding model at 1024 dimensions

**Status**: Accepted (v2 design phase, 2026-05-22)
**Phase**: 1
**Supersedes**: `context/rag-spec.md` (1536-dim `text-embedding-3-small`)

## Context

The MVP picked `text-embedding-3-small` at native 1536 dimensions because the MVP was
single-customer Spanish. The v2 product is generic and multilingual: any deployment may
have docs and queries in es-AR, en-US, pt-BR (or others), often mixed in one corpus.

Cross-language retrieval quality with `text-embedding-3-small` is mediocre (MTEB
multilingual benchmarks show it well below `text-embedding-3-large` and below BGE-M3). The
embedding choice also drives the size of `rag.document_chunks.embedding` and is the most
expensive thing to change retroactively.

## Decision

Fix the schema column to `VECTOR(1024)` and ship two embedding provider implementations
that both produce 1024-dim vectors:

- **`text-embedding-3-large`** with `dimensions=1024` (OpenAI supports truncated dims
  natively, with minimal quality loss). Cloud, OpenAI account required.
- **`BGE-M3`** via self-hosted TEI sidecar. Open-source, multilingual SOTA, but needs a
  GPU for sub-second latency on the indexing path; on CPU it's still workable for
  query-time embedding because queries are single texts.

The setup wizard asks which one to use; default is `text-embedding-3-large` unless the
deployment is "fully on-prem" mode.

## Alternatives considered

### Keep 1536 dims, change model to `text-embedding-3-large` native
Pros: Best OpenAI quality.
Cons: 1.5× the storage and embedding cost for marginal improvement above 1024d in
practice. Bigger pgvector index.

### `multilingual-e5-large` (1024d)
Pros: Open-source, decent multilingual quality.
Cons: Behind BGE-M3 on MTEB multilingual; less momentum in the community.

### Per-locale corpus (one collection per language)
Pros: Could use language-specific models.
Cons: Defeats the "single corpus, mixed languages" requirement. Cross-language queries
(user asks in EN about an ES doc) would need translation pre-step.

### Voyage 3 multilingual
Pros: Top of MTEB benchmarks at decision time.
Cons: Single vendor lock-in, smaller community, cloud-only.

## Consequences

**Positive**
- Single embedding column accommodates two recommended models (OpenAI truncated or BGE-M3).
- Multilingual quality solid (BGE-M3 ranks top 3 on MTEB multilingual; truncated
  text-embedding-3-large stays competitive).
- Provider switching is a configuration change, not a schema change.

**Negative**
- Switching to a higher-quality model that needs more dims (e.g. 1536, 3072) later
  requires a schema migration + reindex.
- Customers who want to try a different model with different dims need a reindex job;
  see Open Question OQ-004.

**Risks / mitigations**
- BGE-M3 on CPU may be too slow for indexing throughput → document recommended hardware,
  ship `compose.gpu.yaml` overlay.
- OpenAI changes the `dimensions=` parameter contract → unlikely (it's been stable since
  release), but the provider wraps it so an SDK change is one file.

## References

- ADR-0001 (provider abstraction)
- ADR-0002 (hybrid retrieval — quality is end-to-end)
- MTEB leaderboard (huggingface.co/spaces/mteb/leaderboard)
- OpenAI embeddings v3 announcement (Jan 2024)
