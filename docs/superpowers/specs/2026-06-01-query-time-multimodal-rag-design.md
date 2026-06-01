# Query-Time Multimodal RAG Design

## Status

Approved on 2026-06-01.

## Context

The first document image slice stores image bytes in private MinIO/S3-compatible object storage and keeps canonical document HTML on stable same-origin URLs such as `/api/document-images/{imageId}/content`. FastAPI currently remains text-first: it indexes document text plus accessible image text (`alt`, `aria-label`, `title`) and captions, but it does not inspect image pixels.

The product now needs query-time multimodal RAG so chat can answer questions whose evidence is present only in an image, screenshot, diagram, or visual table.

## Goals

- Let chat use a limited set of authorized document images as visual evidence.
- Preserve the current retrieval and authorization boundaries: `.NET` owns image metadata, authorization, object storage access, and serving; FastAPI owns RAG retrieval, generation, audit, and budget enforcement.
- Avoid sending every document image to the model.
- Keep provider cost bounded and auditable.
- Keep the first multimodal slice small enough to verify with deterministic tests plus one local Compose/browser acceptance pass.

## Non-Goals

- Do not build visual embeddings or image-vector search.
- Do not generate offline captions during publication in this slice.
- Do not persist image bytes or base64 payloads in Postgres.
- Do not expose raw MinIO/S3 URLs or presigned URLs to FastAPI prompts or browser clients.
- Do not cache multimodal answers in the first slice.
- Do not migrate all provider abstractions or all chat behavior to Responses API in one step.

## Approved Approach

Use query-time multimodal RAG:

1. Continue using textual retrieval over `rag.document_chunks`.
2. During indexing, extract document image references from saved HTML and store a `chunk -> image_id` relationship in the `rag` schema.
3. During chat, retrieve chunks as today, then attach only images associated with the retrieved chunks.
4. FastAPI fetches image bytes through an internal `.NET` endpoint protected by `X-Internal-Service-Token`.
5. FastAPI sends the final textual context plus selected images to OpenAI through the Responses API.
6. Multimodal responses are audited separately and are not written to semantic cache in this slice.

## Data Model

Add a FastAPI-owned table:

```sql
rag.document_chunk_images (
    id uuid primary key,
    chunk_id uuid not null,
    document_id uuid not null,
    document_version_id uuid not null,
    image_id uuid not null,
    ordinal integer not null,
    alt_text text null,
    caption text null,
    created_at timestamptz not null default now(),
    unique (chunk_id, image_id)
)
```

Rules:

- `image_id` is parsed from stable same-origin URLs matching `/api/document-images/{imageId}/content`.
- FastAPI stores only identifiers and optional textual metadata already present in `content_html`.
- Existing rows for a `document_version_id` are deleted and recreated on reindex.
- When chunks for a version are marked inactive, their image associations are no longer eligible because retrieval only selects active chunks.

Add multimodal audit fields to `rag.query_audit_events`:

- `multimodal_used boolean not null default false`
- `multimodal_image_count integer not null default 0`
- `multimodal_image_detail text null`
- `multimodal_image_bytes_total integer not null default 0`
- `multimodal_image_ids jsonb null`

## .NET Internal Image Endpoint

Add an internal-only route:

```http
GET /internal/document-images/{imageId}/content
X-Internal-Service-Token: <secret>
```

FastAPI request query/body inputs:

- `userId`
- `roles`
- optional `corpus`

Behavior:

- Validate the internal service token before any lookup.
- Load image metadata from `app.document_images`.
- Reuse the existing viewer/document authorization path so FastAPI receives bytes only when the current chat user can access the underlying document.
- Return image bytes, `Content-Type`, `Content-Length`, and safe metadata headers.
- Never return raw object keys, MinIO URLs, or presigned URLs.

The public browser image endpoint remains unchanged.

## FastAPI Multimodal Selection

After retrieval and reranking:

1. Query `rag.document_chunk_images` for the final retrieved chunk ids.
2. Keep images in retrieval order, then chunk order, then image ordinal.
3. Deduplicate by `image_id`.
4. Apply hard caps:
   - max images per chat request: `3`
   - max total image bytes sent to OpenAI: `5 MB`
   - allowed content types: PNG, JPEG/JPG, WEBP, non-animated GIF
5. Fetch selected images through `.NET` internal API.
6. If one image fails, skip it, log a warning with request id and image id, and continue.

If no image bytes survive selection, generation falls back to the normal text-only path.

## Provider Strategy

Use the OpenAI Responses API for multimodal generation. Keep the existing Chat Completions path for text-only generation during this slice.

Initial request policy:

- `store: false`
- image inputs as in-memory base64 data URLs
- `detail: "low"` for all images
- answer output remains structured JSON with answer text and cited chunk ids using Responses `text.format`

The configured chat model must support image input. Readiness must fail with `RAG_PROVIDER_MISCONFIGURED` if multimodal is enabled and the configured model cannot support image inputs or lacks active pricing.

## Cache Behavior

Semantic cache lookup remains before retrieval and can serve existing text-only cache hits.

Multimodal answers are not written to semantic cache in this slice because cache keys do not yet include image evidence identity, image bytes hash, detail level, or multimodal provider behavior. This avoids replaying visual answers when image evidence changes or when the answer used costly image inputs.

## Budget And Cost

Budget enforcement remains before paid work. Image inputs are paid model inputs, so they must be included in provider usage and cost audit when the provider reports usage.

The existing cost estimate remains authoritative only if model pricing rows reflect the selected multimodal model. Operators must update `rag.model_pricing` when changing `OPENAI_CHAT_MODEL`.

## Error Handling

- Missing or invalid internal service token: `.NET` returns `AUTH_INTERNAL_TOKEN_INVALID`.
- Image not found or unauthorized: FastAPI skips the image and logs a warning; the chat continues with remaining evidence.
- All selected image fetches fail and the question appears visual: return a safe answer explaining in Spanish that the image could not be inspected.
- OpenAI multimodal provider failure after retries: return `RAG_PROVIDER_UNAVAILABLE`.
- Unsupported configured model: return `RAG_PROVIDER_MISCONFIGURED`.

## Testing Strategy

FastAPI:

- Unit test HTML image reference extraction into chunk image records.
- Integration test indexing persists `rag.document_chunk_images`.
- Unit test image selection caps by image count and total bytes.
- Unit test multimodal generation builds Responses payload with text and image inputs.
- Chat service test confirms multimodal answers are not cached.

.NET:

- API test internal image endpoint rejects missing/invalid service token.
- API test internal image endpoint returns bytes for an authorized user.
- API test internal image endpoint rejects unauthorized document access without exposing image details.

End-to-end:

- Create a document with an image whose content is not described in nearby text.
- Publish/index it.
- Ask a visual question.
- Verify chat answers with citation and audit records `multimodal_used=true`.

## Implementation Slices

1. RAG schema and indexing image references.
2. .NET internal image content endpoint.
3. FastAPI image fetch/selection adapter.
4. OpenAI Responses multimodal provider path.
5. Chat service integration, audit, no-cache rule, and tests.
6. Local Compose/browser acceptance.

## Sources

- OpenAI Images and vision: https://developers.openai.com/api/docs/guides/images-vision
- OpenAI Migrate to Responses API: https://developers.openai.com/api/docs/guides/migrate-to-responses
- OpenAI Responses overview: https://developers.openai.com/api/reference/responses/overview
