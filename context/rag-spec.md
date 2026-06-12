# RAG Specification

This file pins the technical decisions for the FastAPI RAG service. It is the source of truth for chunking, retrieval, generation, caching, citations, and the security-critical `access_scope_hash` algorithm.

`context/architecture.md` defines product-level rules (lifecycle, ownership, cache invalidation, budgets). This file defines how the RAG service implements them.

## Models And Provider

- Text-only chat: OpenAI `gpt-4.1-nano` via `/v1/chat/completions`. Configured by `OPENAI_CHAT_MODEL`.
- Query-time multimodal chat: OpenAI Responses API with text plus image inputs. The configured chat model must support image input when multimodal is enabled.
- Embeddings: `Settings` currently default to OpenAI `text-embedding-3-large` with `dimensions=1024`; `infra/compose/.env.example` sets `OPENAI_EMBEDDING_MODEL=text-embedding-3-small`.
- SDK: official `openai` Python SDK, async client, with `max_retries=2` and `timeout=30` seconds at the SDK level. Service-level retry/circuit-breaker is added with `tenacity` only for transient errors (`APIConnectionError`, `RateLimitError`, `APIStatusError` with 5xx).
- Every paid call (`embeddings.create`, `chat.completions.create`) records the actual model id, usage tokens, latency, and pricing snapshot in `rag.query_audit_events`.

## Chunking

- Input is the normalized HTML stored in `app.document_versions.content_html`.
- MinIO-backed image indexing has two outputs: accessible image text (`alt`, `aria-label`, then `title`) and nearby captions are indexed as ordinary chunk text, and stable same-origin image references are stored as `chunk -> image_id` associations. Indexing must not fetch image bytes or call a multimodal OpenAI endpoint.
- The chunker is HTML-structure-aware: it splits along block boundaries (`<h1>`, `<h2>`, `<h3>`, `<p>`, `<li>`, `<pre>`) before falling back to length-based splits.
- Target chunk size: **500 tokens** measured with `tiktoken` using the model's encoding.
- Hard upper bound: **800 tokens** per chunk (a chunk may exceed 500 if a single block does, up to 800; otherwise it splits).
- Overlap: **80 tokens** carried between consecutive chunks, taken from the tail of the previous chunk.
- Metadata stored per chunk in `rag.document_chunks`:
  - `document_id` (FK to `app.documents.id`; logical reference, not enforced FK across schemas)
  - `document_version_id`
  - `chunk_index` (0-based, contiguous per version)
  - `heading_path` (array of headings, e.g. `["Onboarding", "First Day"]`)
  - `token_count`
  - `char_count`
  - `content` (the chunk text, plain text with light HTML removed)
  - `content_html` (the original HTML fragment, preserved for citation rendering)
  - `embedding` (`Vector(1024)`)
  - `embedding_model` (string snapshot)
  - `created_at`
- Image references associated with each chunk live in `rag.document_chunk_images` with `chunk_id`, `document_id`, `document_version_id`, `image_id`, `ordinal`, optional `alt_text`, optional `caption`, and `created_at`.
- Chunking is deterministic given the same input HTML, the same configured chunker version, and the same target/overlap parameters. The chunker version (`CHUNKER_VERSION` constant, starts at `1`) is recorded on every indexing job for traceability.

## Indexing Pipeline

- Trigger: `.NET` calls `POST /internal/indexing-jobs` with `{document_id, document_version_id, content_html, corpus_mode}` and an internal service token. See `architecture.md` for routing.
- Job lifecycle in `rag.indexing_jobs`: `Pending` â†’ `Running` â†’ `Succeeded` or `Failed`. On failure, `attempts` is incremented, `error_code` and `error_message` are recorded, and `.NET` can request a retry through the same endpoint with `retry=true`.
- A single FastAPI worker process (the same uvicorn workers) consumes jobs synchronously per request for the MVP. A future background worker can be added without changing the contract.
- Steps per job:
  1. Parse and sanitize the incoming `content_html` (defense in depth; `.NET` already sanitizes on save).
  2. Run the HTML-aware chunker. Reject jobs that produce zero chunks with stable code `INDEXING_NO_CONTENT`.
  3. Embed chunks in batches of **100** with the embeddings API. The job records total embedding tokens, latency, model id, dimensions, and pricing snapshot.
  4. Upsert chunks into `rag.document_chunks` (delete existing rows for the same `document_version_id` first, then insert).
  5. Upsert image references into `rag.document_chunk_images` for stable `/api/document-images/{imageId}/content` URLs present in each chunk's HTML fragment.
  6. Mark the job `Succeeded` with `chunk_count` and `embedding_tokens`.
- Pre-publication indexing is synchronous from `.NET`'s perspective: `.NET` keeps the document in `In Review` until the job succeeds.
- Indexing does **not** invalidate semantic cache. Cache invalidation is triggered separately by `.NET` lifecycle events (archive, edit-after-publish, etc.) via a dedicated internal endpoint `POST /internal/cache-invalidations` that takes a list of `document_id`s. See `architecture.md` for the rule.

## Retrieval

- Default `k = 8` chunks per query.
- Similarity metric: cosine distance (`<=>` in pgvector). HNSW index is built on `embedding` with `vector_cosine_ops`.
- Filtering: applied **at SQL level** before similarity ranking. The retrieval query joins `rag.document_chunks` against allowed document records resolved from `.NET`-owned `app.document_permissions` through read-only database grants. FastAPI uses the session validation claims (`role`, `groups`, `corpus`, and `access_scope_hash`) as the user's scope inputs, but it does not receive or trust a precomputed document-id allow list.
- Retrieval applies a lifecycle predicate against `app.documents` (read-only grant): published-corpus chunks are eligible only when the document `current_state` is `Published` **and** the chunk's `document_version_id` equals the document's `current_published_version_id`; preview-corpus chunks require `current_state <> 'Archived'`. `rag.document_chunks.is_active` remains an index-hygiene flag: indexing a version deactivates all prior chunks of the same `(document_id, corpus)`. Correctness never depends on `is_active` alone.
- Retrieval uses hybrid vector/BM25 candidates with optional reranking when a reranker provider is configured; multimodal selection runs after this final retrieval stage.
- For query-time multimodal answers, image candidates are selected only after final text retrieval/reranking. FastAPI selects images associated with the final retrieved chunks, ordered by retrieval order, chunk index, and image ordinal, then deduplicated by `image_id`.
- Initial multimodal caps are 3 images per chat request, 5 MB total image bytes, and OpenAI image `detail: "low"`.
- The retrieved chunks plus their `heading_path` and a short context window (chunk index ±0; no neighbor expansion in MVP) are fed to the chat completion.

## Access Claim (Effective Scope Delivery)

- FastAPI receives the following RAG-relevant claims from the `.NET` internal session validation endpoint:
  - `userId` (user id)
  - `role` (one of `Admin`, `DocumentPublisher`, `DocumentEditor`, `Viewer`)
  - `isGlobalAdmin` (boolean)
  - `organizationalUnitId` (the user's primary organizational-unit id)
  - `groups` (array of group ids, sorted alphabetically)
  - `accessScopeVersion` (monotonic user scope version)
  - `access_scope_hash` (hex SHA-256, see below)
  - `corpus` (one of `published`, `preview`)
- The legacy RS256 chat-token browser flow has been removed. FastAPI runtime should use the `.NET` internal session validation path; remaining token-validator test helpers are a cleanup target, not the production auth path.
- FastAPI validates the CSRF cookie/header pair locally for browser mutations, validates the session through `.NET` on every request that uses `DotnetSessionValidator`, then trusts the returned safe claims as the user's scope inputs for that request only. It uses `access_scope_hash` for cache partitioning and audit, and will use `role`, `isGlobalAdmin`, `organizationalUnitId`, `groups`, and `corpus` for the hierarchical SQL permission filter in the RAG filtering slice.
- `access_scope_hash` is not an authorization mechanism and must never be used by itself to decide whether a chunk is retrievable.
- For the MVP, the retrieval SQL filter resolves group/attribute rules directly against `app.document_permissions` through read-only grants applied by `.NET` EF migrations after the tables exist. FastAPI may also read `app.user_ai_budget_limits` for budget enforcement through the same grant path. These are the only approved FastAPI reads from the `app` schema and are documented in `architecture.md` Operations.
- Organizational-unit scope, branch visibility through `.NET`-owned `app.organizational_unit_closure`, root organizational-unit rules that match every user's organizational-unit dimension, unrestricted global `Admin` scope, and `accessScopeVersion` are RAG-relevant inputs. Non-root organizational-unit rules must use closure-table joins to confirm the document unit and user unit are in the same ancestor/descendant branch, except for `Admin` sessions whose effective scope is explicitly global. Sibling branches must not match. Cache entries created under the previous scope shape must not be reused because V2 hashes differ, and FastAPI must not reuse stale session-claim cache entries after `.NET` changes a user's effective document-access scope. `Admin` global access is represented in the effective scope and hash inputs so semantic-cache partitioning remains explicit.

## `access_scope_hash` Algorithm

Security invariant: two access scopes that differ in any dimension must produce different hashes. The hash is the cache partition key alongside `corpus`.

```text
canonical = {
  "v": 2,
  "role": <user.role>,
  "isGlobalAdmin": <true|false>,
  "organizationalUnitId": <user.organizational_unit_id>,
  "groups": <sorted asc list of group ids>,
  "accessScopeVersion": <user.access_scope_version>
}
serialized = json.dumps(canonical, sort_keys=True, separators=(",", ":"), ensure_ascii=False)
hash       = sha256(serialized.encode("utf-8")).hexdigest()
```

Rules:

- The schema version is currently `v = 2`. Any change to inputs (adding a new attribute family, changing canonical order, changing serialization) **must** bump `v`, invalidate all cache entries with a different `v`, and be recorded as a design decision.
- `groups` values are user-visible group ids (e.g., UUIDs). Use the stable id, not the display name.
- Empty groups are represented as `[]`, not omitted.
- `.NET` computes the V2 hash in `AccessScopeHash.ComputeV2(...)`; FastAPI receives the hash from `.NET` internal session validation and preserves it for semantic-cache and audit partitioning. Contract tests cover the fixed V2 vector and claim shape.
- The MVP does not include `corpus` in the hash. Cache matching uses the tuple `(corpus, access_scope_hash)` instead. This keeps the hash semantically about "who the user is" and separate from "what corpus they are asking against."

## Generation

- When a request includes `session_id`, FastAPI may load the latest bounded set of audited turns for the same `user_id` and same `session_id` before retrieval. The service rewrites follow-up questions into a standalone retrieval query, stores that value as `rewritten_question`, and preserves the original user text in `question`.
- Conversation memory is session-scoped and user-scoped. FastAPI must not use turns from another user or another `session_id`, and it must not use OpenAI-hosted conversation state for MVP memory.
- Retrieval, semantic cache lookup/write, and generation context use the standalone `rewritten_question` when one is produced. User-visible transcript history and audit review continue to show the original `question`.
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
          "required": ["chunk_id", "document_id", "document_version_id", "heading_path"],
          "properties": {
            "chunk_id": { "type": "string" },
            "document_id": { "type": "string" },
            "document_version_id": { "type": "string" },
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
- Multimodal generation uses the OpenAI Responses API with `store: false`, text context plus selected images as input, and structured JSON output through Responses `text.format`. Image bytes are converted to in-memory base64 data URLs for the provider request and are never persisted as base64.

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
- Lookup keys: `(corpus, access_scope_hash, question_embedding)` with `cosine_similarity â‰¥ 0.90` and `expires_at > now()`.
- Cache write happens **only on successful answer with at least one citation**. Answers with empty citations (refusals, "I don't know" cases) are not cached.
- Multimodal answers are not written to semantic cache in the first multimodal slice. Text-only cache lookup may still serve before retrieval; if a multimodal generation path is used, the generated answer bypasses cache write.
- On hit, the response stream emits `cache-hit` followed by the cached `answer` (as a single `answer-token` of the full text or as a fast simulated stream), then the cached `citations`, then a `usage` event with zero new tokens and zero cost.
- Over-budget users skip cache lookup entirely (see `architecture.md` AI Usage Budgets). The chat request returns `AI_BUDGET_EXCEEDED` immediately.
- TTL default: 24 hours (`RAG_SEMANTIC_CACHE_TTL_HOURS`). Threshold default: 0.90 (`RAG_SEMANTIC_CACHE_SIMILARITY_THRESHOLD`).
- Invalidation: any source document mutation (edit-after-publish, archive, restore-to-draft, accessibility change) calls `POST /internal/cache-invalidations` from `.NET`, which deletes cache entries whose `rag.semantic_cache_sources` references one of the affected document ids.

## Indexing And Cache Failure Behavior

- Indexing failure leaves the document in `In Review`. The error is surfaced through the management UI with a stable code and a `Retry indexing` action.
- Cache lookup failure (DB unavailable, embedding error) **degrades gracefully**: skip cache, run the full RAG path, log a warning with the request id.
- OpenAI 429 / 5xx after configured retries returns the shared error envelope with code `RAG_PROVIDER_UNAVAILABLE`. The chat frontend shows a retryable error state.
- OpenAI invalid model or auth error returns `RAG_PROVIDER_MISCONFIGURED` and pages the operator (these should be caught at deploy-time health checks).

## Cost And Audit

- For every chat request (cache hit or miss), `rag.query_audit_events` records:
  - `user_id`, `request_id`, `created_at`
  - `question`, `answer`
  - `session_id`, `previous_event_id`, `rewritten_question`
  - `cache_hit` (boolean), `cached_at` (nullable)
  - `chat_model`, `embedding_model`, `embedding_dimensions`
  - `input_tokens`, `cached_tokens`, `output_tokens`
  - `pricing_snapshot_id` (FK to `rag.model_pricing` row used for cost calculation)
  - `estimated_cost_usd` (numeric, 8 decimal places)
  - `latency_ms` (end-to-end from request received to last SSE event flushed)
  - `access_scope_hash`, `corpus`
  - `prompt_version`, `chunker_version`
  - `feedback_value` (nullable), `feedback_comment` (nullable), `feedback_updated_at` (nullable)
  - `multimodal_used`, `multimodal_image_count`, `multimodal_image_detail`, `multimodal_image_bytes_total`, and `multimodal_image_ids`
- Citations are stored in `rag.query_audit_citations` keyed by `query_audit_event_id`.
- Cost calculation reads the active `rag.model_pricing` row for each model id, snapshots its primary key into the audit row, then computes `cost = input_tokens * input_price + cached_tokens * cached_price + output_tokens * output_price`.
- FastAPI migrations or startup seed logic must ensure active `rag.model_pricing` rows exist for the configured `OPENAI_CHAT_MODEL` and `OPENAI_EMBEDDING_MODEL`.
- FastAPI readiness fails if no active pricing row exists for either configured model. Runtime chat requests also fail safely with `RAG_PROVIDER_MISCONFIGURED` rather than estimating cost as zero or writing incomplete budget evidence.
- Budget enforcement reads `.NET`-owned `app.user_ai_budget_limits` through a read-only database grant and combines it with current-period spend from `rag.query_audit_events`. Budget checks run before semantic cache lookup, query embedding, or chat generation.

## pgvector Index

- Index type: HNSW.
- Operator class: `vector_cosine_ops`.
- Build parameters: `m = 16`, `ef_construction = 64`.
- Two **partial** indexes, one per corpus, each excluding inactive chunks:
  `ix_document_chunks_embedding_hnsw_published` (`WHERE corpus = 'published' AND is_active = true`)
  and `ix_document_chunks_embedding_hnsw_preview` (`WHERE corpus = 'preview' AND is_active = true`).
  Deactivated chunks drop out of the index on vacuum, keeping each index small. The retrieval
  SQL inlines the corpus as a literal (whitelist-validated, not a bound parameter) so the planner
  can prove the partial-index predicate.
- Query-time search depth: `ef_search = 80`, set per query via `SET LOCAL hnsw.ef_search`
  (raised above the pgvector default of 40 so post-filtering on selective access scopes keeps
  recall). When the server's pgvector is `>= 0.8` (detected once at startup), the retrieval query
  also sets `SET LOCAL hnsw.iterative_scan = 'relaxed_order'`, which lets the index scan continue
  past `ef_search` until the `LIMIT` is satisfied — preventing recall collapse for users who can
  access only a small fraction of a large corpus.
- Created by Alembic migrations after the `rag.document_chunks` table exists; the partial indexes
  replace the original global HNSW index (migration `20260611_150000`).
- Indexes are concurrently rebuilt only during a maintenance window; the MVP does not include
  automatic rebuild logic. Re-running the partial-index migration on a large existing
  `rag.document_chunks` table rebuilds the vector indexes, so schedule it in a maintenance window.

## Rate Limits And Backpressure

- Chat: 30 questions/minute/user (see `architecture.md` Operational Defaults).
- Embedding index batch: max 100 chunks/request; if a single chunk exceeds the per-request token limit it falls back to a smaller batch automatically.
- OpenAI 429 backoff uses exponential delay (250 ms, 500 ms, 1000 ms) with jitter, capped at 2 retries before surfacing `RAG_PROVIDER_UNAVAILABLE`.

## What Is Out Of Scope For The MVP

- Query rewriting / HyDE / step-back prompting.
- Visual embeddings or image-vector search.
- Offline visual caption generation during publication.
- Multi-embedding per chunk (e.g., title embedding + body embedding).
- Cross-session memory, cross-user memory, and OpenAI-hosted conversation state. The MVP only supports bounded same-user memory within an explicit `session_id`.
- Tool/function calling beyond the structured-output schema.
- Exact (no-cost) cache lookup for over-budget users. Deferred until needed.
