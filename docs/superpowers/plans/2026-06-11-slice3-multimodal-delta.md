# Slice 3: Query-Time Multimodal RAG — Execution Delta

> **For agentic workers:** This file is NOT a standalone plan. Execute
> `docs/superpowers/plans/2026-06-01-query-time-multimodal-rag-implementation-plan.md`
> task-by-task with superpowers:executing-plans, applying the overrides below. The
> overrides exist because the codebase moved between 2026-06-01 (when that plan was
> written) and 2026-06-11. Where this delta and the original plan conflict, **this
> delta wins**. The design itself is unchanged and approved
> (`docs/superpowers/specs/2026-06-01-query-time-multimodal-rag-design.md`).

**Goal:** Implement query-time multimodal RAG: store `chunk -> image_id` references at indexing time, select images only from final retrieved chunks (caps: 3 images, 5 MB total, `detail: "low"`), fetch authorized bytes through a new internal `.NET` endpoint, and generate via OpenAI Responses API with `store: false`. Multimodal answers are never written to the semantic cache.

**Verified still true on 2026-06-11:** none of the original plan is implemented. There is no `rag.document_chunk_images` table, no `multimodal_*` audit columns, no image selection code, no Responses API call, and no internal `.NET` image content endpoint (`DocumentImagesController.cs` has no internal route). Image bytes live in MinIO via `S3DocumentImageObjectStorage.cs`.

---

## Override 1 (Task 1): Alembic revision chain

The original plan's migration `20260601_120000_add_multimodal_image_references.py` predates two migrations that have since landed. The new migration MUST chain after the current head:

```python
revision: str = "20260611_130000"
down_revision: str | None = "20260610_120000"  # add_scope_document_id_to_query_audit
```

Name the file `20260611_130000_add_multimodal_image_references.py`. Everything else in Task 1 (table shape, `multimodal_*` audit columns, migration tests) applies unchanged. Before writing it, confirm the head with:

Run: `uv run alembic heads` (from `services/rag-api`)
Expected: `20260610_120000`.

## Override 2 (Task 2): The chunker destroys `<img>` tags — extract references during parsing, not from `content_html`

The original plan's Task 2 parses stable image URLs out of each chunk's `content_html`. **That no longer works**: `chunking._create_chunk` rebuilds `content_html` as escaped plain text (`f"<p>{escape(normalized)}</p>"`), so `<img>` tags never survive into stored chunks. Instead, collect image references inside the HTML parser:

In `services/rag-api/src/advanced_rag/rag/chunking.py`:

```python
import re

STABLE_IMAGE_SRC_PATTERN = re.compile(
    r"^/api/document-images/(?P<image_id>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}"
    r"-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})/content$"
)


class ChunkImageRef(BaseModel):
    model_config = ConfigDict(frozen=True)

    image_id: UUID
    ordinal: int  # 0-based order of appearance within the chunk
    alt_text: str | None = None
```

- `DocumentChunk` gains `images: list[ChunkImageRef] = []`.
- `_Block` gains `images: list[tuple[UUID, str | None]]` (image id + alt text, in order).
- In `_HtmlBlockParser.handle_starttag`, when `tag == "img"`: besides the existing alt-text buffering, read the `src` attribute, match it against `STABLE_IMAGE_SRC_PATTERN`, and on match append `(UUID(match["image_id"]), alt_or_none)` to the current block's images. Non-matching `src` values (external URLs) are ignored — only same-origin stable references are indexable, mirroring `.NET`'s `StableDocumentImageSourcePattern`.
- In `chunk_html`, accumulate the images of every block consumed into the current chunk; when a chunk is emitted, attach the accumulated refs with sequential `ordinal` values and reset the accumulator. For an oversized block split by `_split_long_text`, attach the block's images to the **first** chunk produced from it. Overlap tails carry **no** images (no duplication).
- `caption` stays unpopulated in this slice (`figcaption` text already flows into chunk text); insert `NULL`.

Tests for this override (in `tests/test_chunking.py`): a chunk containing two stable images yields two ordered refs with correct UUIDs and alt texts; an external `src` yields no ref; a split oversized block attaches images only to its first chunk.

The original Task 2's indexing-service change still applies as written: insert one `rag.document_chunk_images` row per `(chunk, image)` inside the same transaction as the chunk inserts, after the chunk loop in `indexing_service.create_job`.

## Override 3 (Task 3): .NET internal image endpoint — unchanged but confirm token plumbing

Task 3 stands as written (add `GET /internal/document-images/{imageId}/content` to `DocumentImagesController`, protected by `X-Internal-Service-Token`). Note the controller currently has no internal-token validation helper; copy the token comparison approach used by FastAPI-bound internal calls in `Program.cs` (the same compose secret is available to `.NET`). The endpoint must return image bytes + content type and `404` for unknown ids, with **no session/cookie auth** (internal network only, never routed through Caddy).

## Override 4 (Task 4): config fields — exact names

Add to `services/rag-api/src/advanced_rag/core/config.py` (Settings):

```python
multimodal_enabled: bool = True
multimodal_max_images: int = 3
multimodal_max_total_image_bytes: int = 5242880  # 5 MB
multimodal_image_detail: str = "low"
dotnet_internal_base_url: str = "http://dotnet-api:8080"
```

The image fetcher reuses `resolved_internal_service_token` for the outbound header. Document the new env vars in `infra/compose/.env.example` with these defaults.

## Override 5 (Task 6): chat_service integration points moved

`chat_service.answer()` has grown since the original plan (session memory, rewritten questions, `scope_document_id` doc-scoped mini chat, hybrid retrieval + rerank). Apply Task 6 like this:

- Image selection runs **after** `_retrieve_chunks(...)` returns (that result is already post-rerank, final top-K) and only when `self._settings.multimodal_enabled and chunks` — for both the global chat and the doc-scoped mini chat.
- Multimodal generation replaces the `generate_answer(...)` call only when at least one image survived selection/caps; otherwise the existing text path runs untouched.
- Cache rule: the existing write condition `if citations and scope_document_id is None:` becomes `if citations and scope_document_id is None and not multimodal_used:`. Cache **lookup** stays before retrieval, unchanged (text-only lookup may still serve, per spec).
- `_insert_audit` gains the five new parameters and columns appended to the INSERT column/values lists: `multimodal_used` (bool), `multimodal_image_count` (int), `multimodal_image_detail` (text, nullable), `multimodal_image_bytes_total` (int), `multimodal_image_ids` (jsonb array of uuid strings, nullable). Every existing call site passes the text-only defaults (`False, 0, None, 0, None`).
- Image bytes are fetched concurrently (`asyncio.gather`) from `.NET`; any single fetch failure drops that image and logs a warning — it must not fail the chat request. If all fetches fail, fall back to the text-only path.

## Override 6 (Task 5): provider surface

Keep `ILlmProvider` untouched. Add to `providers/base.py` a separate protocol so text-only providers (ollama, anthropic, azure) need no changes:

```python
class ImageInput(BaseModel):
    model_config = ConfigDict(frozen=True)

    data_base64: str          # raw base64, no data: prefix
    media_type: str           # e.g. "image/png"
    detail: str = "low"


class IMultimodalLlmProvider(Protocol):
    name: str

    async def multimodal_complete(
        self,
        req: ChatCompletionRequest,
        images: list[ImageInput],
    ) -> tuple[str, ChatUsage]:
        ...
```

`OpenAiProvider` implements it via the Responses API (`client.responses.create`, `store=False`, message content mixing `input_text` and `input_image` parts with `detail`, JSON output via `text.format`). The chat service checks `isinstance`-style capability (`getattr(self._llm_provider, "multimodal_complete", None)`) and falls back to text-only when the configured provider lacks it.

## Everything Else

Tasks 1, 7, 8 of the original plan apply as written (with Override 1's revision id). Original Task acceptance, verification commands, and the user-owned Compose acceptance checkpoint are unchanged. After completion update `context/rag-spec.md` only if implementation deviated from the spec (the spec already describes this feature), update `context/progress-tracker.md`, mark Slice 3 in the roadmap, and run `graphify update .`.

## Postman Checklist (new/changed endpoints)

- GET `http://dotnet-api:8080/internal/document-images/{imageId}/content` — internal network only; header `X-Internal-Service-Token: <token>`; expect `200` with image bytes + correct `Content-Type`; `401` without token; `404` unknown id.
- POST `https://chat.<host>/api/chat` — unchanged contract; verify `rag.query_audit_events.multimodal_used = true` and `multimodal_image_count <= 3` after a visual question over a published document with a stable image.
