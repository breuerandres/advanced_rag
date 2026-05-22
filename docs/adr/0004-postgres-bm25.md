# ADR-0004 — BM25 / lexical search in Postgres (no Elasticsearch)

**Status**: Accepted (v2 design phase, 2026-05-22)
**Phase**: 2

## Context

The hybrid retrieval pipeline (ADR-0002) needs a lexical retriever alongside the vector
kNN. The classic choice is BM25 in Elasticsearch or OpenSearch. But adding ES means:

- A new container with significant memory footprint (~2 GB baseline JVM).
- A sync pipeline keeping ES in lockstep with Postgres on every chunk write.
- Operations cost (snapshots, version upgrades, security patches).
- One more thing to teach the customer's sysadmin.

The v2 product is "single-tenant, Docker Compose, low-ops". Postgres is already there,
already extended with pgvector. The question is whether Postgres-native lexical search is
good enough.

## Decision

Use Postgres `tsvector` + `pg_trgm` + `unaccent` for lexical retrieval, combined with the
vector kNN via RRF (ADR-0002).

Migration adds to `rag.document_chunks`:

```sql
ALTER TABLE rag.document_chunks
  ADD COLUMN content_tsv tsvector GENERATED ALWAYS AS
    (to_tsvector('simple', unaccent(content))) STORED,
  ADD COLUMN language TEXT;

CREATE INDEX ix_chunks_tsv  ON rag.document_chunks USING GIN (content_tsv);
CREATE INDEX ix_chunks_trgm ON rag.document_chunks USING GIN (content gin_trgm_ops);
```

`'simple'` configuration is intentional: it doesn't stem, doesn't drop stopwords, doesn't
do language-specific normalisation. We pair it with `unaccent` to normalise accents. This
keeps the configuration language-agnostic (critical for multilingual corpora; see
ADR-0003).

`pg_trgm` GIN index on raw `content` gives us fuzzy matching for codes and identifiers
that `tsvector` would tokenise incorrectly (e.g. `IMA001` becomes `ima001` token, which
is fine; but `IMA-001-A` benefits from trigram).

The retrieval SQL uses `ts_rank_cd` on `content_tsv` + similarity threshold on
`pg_trgm.similarity`. Both contribute to the BM25-side candidate list before RRF.

## Alternatives considered

### Elasticsearch / OpenSearch
Pros: Best-in-class BM25, mature ecosystem.
Cons: Operational overhead disproportionate to the help-center document volumes typical
of this product (≤10K docs, ≤500K chunks per tenant). Extra container, JVM, sync code.

### `paradedb` (Postgres extension with real BM25)
Pros: True BM25 inside Postgres, very fast.
Cons: Not as widely deployed as `tsvector`. Less battle-tested; some Postgres providers
don't allow loading custom extensions.

### MeiliSearch / Typesense
Pros: Lightweight, designed for SaaS, fast.
Cons: Another container; ad-hoc sync code; not RAG-optimised.

### `tsvector` with full language-specific configuration
Pros: Better recall per single language.
Cons: Multilingual is harder (need per-language `tsvector` columns or runtime config
switching). The `'simple'` + `unaccent` combo is good enough cross-language for our
use case.

## Consequences

**Positive**
- Zero new infrastructure.
- Permissions and filters apply to BM25 the same way they apply to vector (single SQL
  query joins both candidate sets).
- Cache-friendly: BM25 results live in the same DB as the embedding cache.
- Multilingual without per-language config explosion.

**Negative**
- Quality below pure ES on long-form natural-language queries (mitigated by reranker;
  see ADR-0002).
- `ts_rank_cd` is not BM25 exactly — close enough for hybrid retrieval where ranking is
  fused, not used directly.
- GIN index size grows with corpus; ≤500K chunks expected to stay below 2 GB total index.

**Risks / mitigations**
- If a customer's corpus is dominated by code or extreme term sparsity → quality drop on
  lexical side. Mitigation: document `paradedb` as the upgrade path; not on by default.
- Postgres tsvector index rebuild after schema/extension upgrade can be slow → migration
  uses `CREATE INDEX CONCURRENTLY` after the column is added.

## References

- ADR-0002 (hybrid retrieval pipeline)
- Postgres tsvector documentation
- ParadeDB project (https://github.com/paradedb/paradedb)
