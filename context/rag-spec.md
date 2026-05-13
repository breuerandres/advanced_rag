# RAG Specification

This file pins the technical decisions for the FastAPI RAG service. It is the source of truth for chunking, retrieval, generation, caching, citations, and the security-critical `access_scope_hash` algorithm.

`context/architecture.md` defines product-level rules (lifecycle, ownership, cache invalidation, budgets). This file defines how the RAG service implements them.

## Models And Provider

- Chat: OpenAI `gpt-4.1-mini` via `/v1/chat/completions`. Configured by `OPENAI_CHAT_MODEL`.
- Embeddings: OpenAI `text-embedding-3-large` with `dimensions=1536`. Configured by `OPENAI_EMBEDDING_MODEL` and `OPENAI_EMBEDDING_DIMENSIONS`.
- SDK: official `openai` Python SDK, async client, with `max_retries=2` and `timeout=30` seconds at the SDK level. Service-level retry/circuit-breaker is added with `tenacity` only for transient errors (`APIConnectionError`, `RateLimitError`, `APIStatusError` with 5xx).
- Every paid call (`embeddings.create`, `chat.completions.create`) records the actual model id, usage tokens, latency, and pricing snapshot in `rag.query_audit_events`.

## Chunking

- Input is the normalized HTML stored in `app.instruction_versions.content_html`.
- The chunker is HTML-structure-aware: it splits along block boundaries (`<h1>`, `<h2>`, `<h3>`, `<p>`, `<li>`, `<pre>`) before falling back to length-based splits.
- Target chunk size: **500 tokens** measured with `tiktoken` using the model's encoding.
- Hard upper bound: **800 tokens** per chunk (a chunk may exceed 500 if a single block does, up to 800; otherwise it splits).
- Overlap: **80 tokens** carried between consecutive chunks, taken from the tail of the previous chunk.
- Metadata stored per chunk in `rag.document_chunks`:
  - `document_id` (FK to `app.instructions.id`; logical reference, not enforced FK across schemas)
  - `instruction_version_id`
  - `chunk_index` (0-based, contiguous per version)
  - `heading_path` (array of headings, e.g. `["Onboarding", "First Day"]`)
  - `token_count`
  - `char_count`
  - `content` (the chunk text, plain text with light HTML removed)
  - `content_html` (the original HTML fragment, preserved for citation rendering)
  - `embedding` (`Vector(1536)`)
  - `embedding_model` (string snapshot)
  - `created_at`
- Chunking is deterministic given the same input HTML, the same configured chunker version, and the same target/overlap parameters. The chunker version (`CHUNKER_VERSION` constant, starts at `1`) is recorded on every indexing job for traceability.

## Indexing Pipeline

- Trigger: `.NET` calls `POST /internal/indexing-jobs` with `{instruction_id, instruction_version_id, content_html, corpus_mode}` and an internal service token. See `architecture.md` for routing.
- Job lifecycle in `rag.indexing_jobs`: `Pending` → `Running` → `Succeeded` or `Failed`. On failure, `attempts` is incremented, `error_code` and `error_message` are recorded, and `.NET` can request a retry through the same endpoint with `retry=true`.
- A single FastAPI worker process (the same uvicorn workers) consumes jobs synchronously per request for the MVP. A future background worker can be added without changing the contract.
- Steps per job:
  1. Parse and sanitize the incoming `content_html` (defense in depth; `.NET` already sanitizes on save).
  2. Run the HTML-aware chunker. Reject jobs that produce zero chunks with stable code `INDEXING_NO_CONTENT`.
  3. Embed chunks in batches of **100** with the embeddings API. The job records total embedding tokens, latency, model id, dimensions, and pricing snapshot.
  4. Upsert chunks into `rag.document_chunks` (delete existing rows for the same `instruction_version_id` first, then insert).
  5. Mark the job `Succeeded` with `chunk_count` and `embedding_tokens`.
- Pre-publication indexing is synchronous from `.NET`'s perspective: `.NET` keeps the document in `In Review` until the job succeeds.
- Indexing does **not** invalidate semantic cache. Cache invalidation is triggered separately by `.NET` lifecycle events (archive, edit-after-publish, etc.) via a dedicated internal endpoint `POST /internal/cache-invalidations` that takes a list of `instruction_id`s. See `architecture.md` for the rule.

## Retrieval

- Default `k = 8` chunks per query.
- Similarity metric: cosine distance (`<=>` in pgvector). HNSW index is built on `embedding` with `vector_cosine_ops`.
- Filtering: applied **at SQL level** before similarity ranking. The retrieval query joins `rag.document_chunks` against an `effective_documents` filter computed from the user's role, groups, and attributes (`.NET` exposes the effective document id list to FastAPI via a signed claim in the chat access token; see Access Claim below).
- Only chunks belonging to the latest successfully indexed version of each allowed document are eligible (a `rag.document_chunks.is_active` boolean defaulted to `true` and flipped to `false` when a newer version supersedes the prior version's chunks).
- No reranking step in the MVP. A future cross-encoder rerank stage is the natural next optimization once retrieval quality metrics exist.
- The retrieved chunks plus their `heading_path` and a short context window (chunk index ±0; no neighbor expansion in MVP) are fed to the chat completion.

## Access Claim (Effective Scope Delivery)

- `.NET` issues the chat access token (JWT, RS256) with the following claims relevant to RAG:
  - `sub` (user id)
  - `role` (one of `Admin`, `DocumentManager`, `Viewer`)
  - `groups` (array of group ids, sorted alphabetically)
  - `attributes` (object of `string → string`, keys sorted alphabetically)
  - `access_scope_hash` (hex SHA-256, see below)
  - `corpus` (one of `published`, `preview`)
  - `exp`, `iat`, `iss`, `aud`, `jti`, `kid` (header)
- FastAPI never recomputes effective scope from the database. It trusts the claims and uses `access_scope_hash` for cache matching and `role` + `groups` + `attributes` + `corpus` for the SQL filter.
- For the MVP, the SQL filter resolves group/attribute rules directly against `app.instruction_permissions` through a read-only grant; this is the only `app` read from FastAPI and is documented as such in `architecture.md` Operations.

## `access_scope_hash` Algorithm

Security invariant: two access scopes that differ in any dimension must produce different hashes. The hash is the cache partition key alongside `corpus`.

```text
canonical = {
  "v": 1,
  "role": <user.role>,
  "groups": <sorted asc list of group ids>,
  "attributes": {<sorted asc keys>: <attribute value>}
}
serialized = json.dumps(canonical, sort_keys=True, separators=(",", ":"), ensure_ascii=False)
hash       = sha256(serialized.encode("utf-8")).hexdigest()
```

Rules:

- The schema version `v` starts at `1`. Any change to inputs (adding a new attribute family, changing canonical order, changing serialization) **must** bump `v`, invalidate all cache entries with a different `v`, and be recorded as a design decision.
- `groups` values are user-visible group ids (e.g., UUIDs). Use the stable id, not the display name.
- `attributes` values are strings. Numbers, booleans, and nulls are converted to their canonical string form before hashing (e.g., `true` → `"true"`).
- Empty groups or attributes are represented as `[]` and `{}` respectively, not omitted.
- `.NET` and FastAPI must produce the same hash for the same logical scope. A contract test (`AccessScopeHashCompatibilityTests` in .NET, `test_access_scope_hash.py` in FastAPI) covers fixed scope inputs and compares against pre-computed expected hex digests.
- The MVP does not include `corpus` in the hash. Cache matching uses the tuple `(corpus, access_scope_hash)` instead. This keeps the hash semantically about "who the user is" and separate from "what corpus they are asking against."

## Generation

- Chat completion is called with `temperature=0.1`, `top_p=1.0`, `presence_penalty=0`, `frequency_penalty=0`, `max_tokens=900` for the MVP. All configurable via environment.
- Streaming is **on** for chat completions. The FastAPI endpoint returns Server-Sent Events.
- Citations are produced via OpenAI **structured outputs** (`response_format={"type": "json_schema", ...}`) using this schema:

```json
{
  "name": "AssistantAnswer",
  "schema": {
    "type": "object",
    "additionalProperties": false,
    "required": ["answer", "citations"],
    "properties": {
      "answer": { "type": "string" },
      "citations": {
        "type": "array",
        "items": {
          "type": "object",
          "additionalProperties": false,
          "required": ["chunk_id", "document_id", "instruction_version_id", "heading_path"],
          "properties": {
            "chunk_id": { "type": "string" },
            "document_id": { "type": "string" },
            "instruction_version_id": { "type": "string" },
            "heading_path": { "type": "array", "items": { "type": "string" } }
          }
        }
      }
    }
  }
}
```

- Structured output is **incompatible with token streaming** in some model/version combinations. For the MVP, the implementation streams the `answer` token-by-token using a custom server-side parser that watches for the `"answer":"..."` field and emits its character deltas as SSE `event: answer-token`, then emits one `event: citations` payload when streaming completes, followed by `event: done`. If a future OpenAI version supports streaming structured outputs natively, switch to that.
- The system prompt template is stored in `services/rag-api/src/advanced_rag/rag/prompts/system_v1.md` and loaded at process startup. The active prompt version (`PROMPT_VERSION`, starts at `1`) is recorded per audit row.
- The user-facing answer is in **Spanish (es-AR)**. The system prompt explicitly instructs the model to answer in Spanish regardless of the question language, and to refuse politely if the retrieved context cannot support an answer.

### SSE Event Types

| Event | Payload | When |
| --- | --- | --- |
| `request-id` | `{ "request_id": "..." }` | First, before any tokens |
| `cache-hit` | `{ "cached_at": "ISO-8601" }` | Only when serving from semantic cache; replaces token streaming |
| `answer-token` | `{ "delta": "..." }` | For each generated character chunk during streaming |
| `citations` | `{ "citations": [...] }` | After the answer terminates |
| `usage` | `{ "input_tokens": N, "cached_tokens": N, "output_tokens": N, "cost_usd": N }` | After citations, before `done` |
| `done` | `{}` | Stream complete |
| `error` | `{ "error": { "code": "...", "message": "...", "request_id": "..." } }` | On error; terminates the stream |

## Semantic Cache

- Entries live in `rag.semantic_cache_entries`. Source documents per entry live in `rag.semantic_cache_sources`.
- Lookup keys: `(corpus, access_scope_hash, question_embedding)` with `cosine_similarity ≥ 0.90` and `expires_at > now()`.
- Cache write happens **only on successful answer with at least one citation**. Answers with empty citations (refusals, "I don't know" cases) are not cached.
- On hit, the response stream emits `cache-hit` followed by the cached `answer` (as a single `answer-token` of the full text or as a fast simulated stream), then the cached `citations`, then a `usage` event with zero new tokens and zero cost.
- Over-budget users skip cache lookup entirely (see `architecture.md` AI Usage Budgets). The chat request returns `AI_BUDGET_EXCEEDED` immediately.
- TTL default: 24 hours (`RAG_SEMANTIC_CACHE_TTL_HOURS`). Threshold default: 0.90 (`RAG_SEMANTIC_CACHE_SIMILARITY_THRESHOLD`).
- Invalidation: any source document mutation (edit-after-publish, archive, restore-to-draft, accessibility change) calls `POST /internal/cache-invalidations` from `.NET`, which deletes cache entries whose `rag.semantic_cache_sources` references one of the affected instruction ids.

## Indexing And Cache Failure Behavior

- Indexing failure leaves the document in `In Review`. The error is surfaced through the management UI with a stable code and a `Retry indexing` action.
- Cache lookup failure (DB unavailable, embedding error) **degrades gracefully**: skip cache, run the full RAG path, log a warning with the request id.
- OpenAI 429 / 5xx after configured retries returns the shared error envelope with code `RAG_PROVIDER_UNAVAILABLE`. The chat frontend shows a retryable error state.
- OpenAI invalid model or auth error returns `RAG_PROVIDER_MISCONFIGURED` and pages the operator (these should be caught at deploy-time health checks).

## Cost And Audit

- For every chat request (cache hit or miss), `rag.query_audit_events` records:
  - `user_id`, `request_id`, `created_at`
  - `question`, `answer`
  - `cache_hit` (boolean), `cached_at` (nullable)
  - `chat_model`, `embedding_model`, `embedding_dimensions`
  - `input_tokens`, `cached_tokens`, `output_tokens`
  - `pricing_snapshot_id` (FK to `rag.model_pricing` row used for cost calculation)
  - `estimated_cost_usd` (numeric, 8 decimal places)
  - `latency_ms` (end-to-end from request received to last SSE event flushed)
  - `access_scope_hash`, `corpus`
  - `prompt_version`, `chunker_version`
  - `feedback_value` (nullable), `feedback_comment` (nullable), `feedback_updated_at` (nullable)
- Citations are stored in `rag.query_audit_citations` keyed by `query_audit_event_id`.
- Cost calculation reads the active `rag.model_pricing` row for each model id, snapshots its primary key into the audit row, then computes `cost = input_tokens * input_price + cached_tokens * cached_price + output_tokens * output_price`.

## pgvector Index

- Index type: HNSW.
- Operator class: `vector_cosine_ops`.
- Parameters: `m = 16`, `ef_construction = 64` (build time), `ef_search = 40` (query time, set per session via `SET hnsw.ef_search`).
- Created by an Alembic migration after the `rag.document_chunks` table exists.
- The index is concurrently rebuilt only during a maintenance window; the MVP does not include automatic rebuild logic.

## Rate Limits And Backpressure

- Chat: 30 questions/minute/user (see `architecture.md` Operational Defaults).
- Embedding index batch: max 100 chunks/request; if a single chunk exceeds the per-request token limit it falls back to a smaller batch automatically.
- OpenAI 429 backoff uses exponential delay (250 ms, 500 ms, 1000 ms) with jitter, capped at 2 retries before surfacing `RAG_PROVIDER_UNAVAILABLE`.

## What Is Out Of Scope For The MVP

- Reranking (cross-encoder or third-party rerank API).
- Hybrid lexical + vector search.
- Query rewriting / HyDE / step-back prompting.
- Multi-modal retrieval (images, tables as images, OCR).
- Multi-embedding per chunk (e.g., title embedding + body embedding).
- Conversation memory across user sessions; each chat question is independent in the MVP.
- Tool/function calling beyond the structured-output schema.
- Exact (no-cost) cache lookup for over-budget users. Deferred until needed.
