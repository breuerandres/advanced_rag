# Query-Time Multimodal RAG Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement query-time multimodal RAG so chat can attach a capped set of authorized document images from retrieved chunks and use OpenAI Responses API for visual questions.

**Architecture:** Keep textual retrieval as the primary filter. FastAPI stores `chunk -> image_id` references during indexing, selects images only from final retrieved chunks, fetches authorized bytes through an internal `.NET` endpoint, and uses OpenAI Responses API only for multimodal generation. Text-only chat remains on the existing provider path during this slice.

**Tech Stack:** FastAPI, SQLAlchemy async, Alembic, PostgreSQL/pgvector, ASP.NET Core 8 MVC, EF Core, AWSSDK.S3/MinIO, official OpenAI Python SDK, pytest, xUnit, `uv`, `dotnet`, Docker Compose.

---

## Human-Owned Checkpoints

- User-owned secrets remain unchanged. Do not create or commit real OpenAI, S3, database, JWT, CSRF, or internal service tokens.
- User-owned local acceptance happens after implementation: run the Compose stack with a real local OpenAI key, publish a document with an image whose visual evidence is not repeated in surrounding text, ask a visual question, and report the chat answer plus whether `rag.query_audit_events.multimodal_used` is true.
- Agent-owned work is code, migrations, deterministic tests, documentation/context updates, and local static verification.

## File And Responsibility Map

### FastAPI RAG

- Modify: `services/rag-api/src/advanced_rag/rag/chunking.py`
  - Return chunk image references along with chunk text.
- Modify: `services/rag-api/src/advanced_rag/rag/indexing_service.py`
  - Persist `rag.document_chunk_images` rows in the same indexing transaction as chunks.
- Create: `services/rag-api/src/advanced_rag/rag/image_references.py`
  - Parse stable `/api/document-images/{imageId}/content` URLs from chunk HTML and normalize image metadata.
- Create: `services/rag-api/src/advanced_rag/rag/multimodal_images.py`
  - Select image references for retrieved chunks, deduplicate them, fetch bytes, enforce caps, and produce provider-ready inputs.
- Modify: `services/rag-api/src/advanced_rag/rag/chat_service.py`
  - Integrate image selection/fetching, invoke multimodal generation when images are available, audit multimodal fields, and skip semantic cache writes for multimodal answers.
- Modify: `services/rag-api/src/advanced_rag/rag/answer_generator.py`
  - Add a multimodal generation path that calls the provider abstraction with image inputs.
- Modify: `services/rag-api/src/advanced_rag/providers/base.py`
  - Add provider-agnostic image input request types.
- Modify: `services/rag-api/src/advanced_rag/providers/openai_provider.py`
  - Implement Responses API multimodal completion with `store: false`, `text.format`, and `input_image` parts.
- Modify: `services/rag-api/src/advanced_rag/core/config.py`
  - Add multimodal caps and `.NET` internal image endpoint config.
- Create: `services/rag-api/alembic/versions/20260601_120000_add_multimodal_image_references.py`
  - Add `rag.document_chunk_images` and multimodal audit columns.
- Tests:
  - Modify/Create: `services/rag-api/tests/test_chunking.py`
  - Modify: `services/rag-api/tests/test_indexing.py`
  - Create: `services/rag-api/tests/test_multimodal_images.py`
  - Create: `services/rag-api/tests/test_openai_responses_multimodal.py`
  - Modify: `services/rag-api/tests/test_chat_rag.py`
  - Modify: `services/rag-api/tests/test_migrations.py`

### .NET API

- Modify: `services/dotnet-api/src/AdvancedRag.Api/Controllers/DocumentImagesController.cs`
  - Add internal image content endpoint protected by `X-Internal-Service-Token`.
- Modify: `services/dotnet-api/src/AdvancedRag.App/DocumentImages/DocumentImageTypes.cs`
  - Add command/result types for internal image fetch if separating public and internal command semantics improves clarity.
- Tests:
  - Modify: `services/dotnet-api/tests/AdvancedRag.Api.Tests/DocumentImageEndpointTests.cs`
  - Modify if needed: `services/dotnet-api/tests/AdvancedRag.App.Tests/DocumentLifecycleServiceTests.cs`

### Context And Docs

- Modify: `context/progress-tracker.md`
  - Record slice progress, verification, user-owned acceptance checkpoint.
- Modify: `context/design-decisions.md`
  - Only update if implementation changes the approved design.

## Task 1: RAG Schema For Chunk Image References And Multimodal Audit

**Files:**
- Create: `services/rag-api/alembic/versions/20260601_120000_add_multimodal_image_references.py`
- Modify: `services/rag-api/tests/test_migrations.py`

- [ ] **Step 1: Write failing migration tests**

Add tests in `services/rag-api/tests/test_migrations.py` that apply Alembic head to a pgvector Postgres container and assert:

```python
async def _read_multimodal_schema(dsn: str) -> dict[str, object]:
    connection = await asyncpg.connect(dsn)
    try:
        image_table = await connection.fetchval(
            """
            select to_regclass('rag.document_chunk_images') is not null
            """
        )
        image_columns = await connection.fetch(
            """
            select column_name
            from information_schema.columns
            where table_schema = 'rag'
              and table_name = 'document_chunk_images'
            order by ordinal_position
            """
        )
        audit_columns = await connection.fetch(
            """
            select column_name
            from information_schema.columns
            where table_schema = 'rag'
              and table_name = 'query_audit_events'
              and column_name like 'multimodal_%'
            order by column_name
            """
        )
    finally:
        await connection.close()

    return {
        "image_table": bool(image_table),
        "image_columns": [row["column_name"] for row in image_columns],
        "audit_columns": [row["column_name"] for row in audit_columns],
    }


def test_migrations_create_multimodal_image_reference_schema() -> None:
    with PostgresContainer(
        image=POSTGRES_IMAGE,
        username=POSTGRES_USER,
        password=POSTGRES_PASSWORD,
        dbname=POSTGRES_DB,
    ) as postgres:
        host = postgres.get_container_host_ip()
        port = postgres.get_exposed_port(5432)
        async_url = _async_sqlalchemy_url(host, port)
        asyncpg_dsn = _asyncpg_dsn(host, port)
        asyncio.run(_bootstrap_rag_schema(asyncpg_dsn))
        _run_migrations(async_url)
        schema = asyncio.run(_read_multimodal_schema(asyncpg_dsn))

    assert schema["image_table"] is True
    assert schema["image_columns"] == [
        "id",
        "chunk_id",
        "document_id",
        "document_version_id",
        "image_id",
        "ordinal",
        "alt_text",
        "caption",
        "created_at",
    ]
    assert schema["audit_columns"] == [
        "multimodal_image_bytes_total",
        "multimodal_image_count",
        "multimodal_image_detail",
        "multimodal_image_ids",
        "multimodal_used",
    ]
```

- [ ] **Step 2: Run the migration test and verify RED**

Run:

```powershell
Set-Location services\rag-api
uv run pytest tests/test_migrations.py -q
Set-Location ..\..
```

Expected: failure because `rag.document_chunk_images` and the multimodal audit columns do not exist.

- [ ] **Step 3: Create the Alembic migration**

Create `services/rag-api/alembic/versions/20260601_120000_add_multimodal_image_references.py` with:

```python
"""add multimodal image references"""

from alembic import op
import sqlalchemy as sa


revision = "20260601_120000"
down_revision = "20260522_134100"
branch_labels = None
depends_on = None


def upgrade() -> None:
    op.create_table(
        "document_chunk_images",
        sa.Column("id", sa.Uuid(), primary_key=True),
        sa.Column("chunk_id", sa.Uuid(), nullable=False),
        sa.Column("document_id", sa.Uuid(), nullable=False),
        sa.Column("document_version_id", sa.Uuid(), nullable=False),
        sa.Column("image_id", sa.Uuid(), nullable=False),
        sa.Column("ordinal", sa.Integer(), nullable=False),
        sa.Column("alt_text", sa.Text(), nullable=True),
        sa.Column("caption", sa.Text(), nullable=True),
        sa.Column("created_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.text("now()")),
        sa.UniqueConstraint("chunk_id", "image_id", name="uq_document_chunk_images_chunk_image"),
        schema="rag",
    )
    op.create_index(
        "ix_document_chunk_images_chunk_id",
        "document_chunk_images",
        ["chunk_id"],
        schema="rag",
    )
    op.create_index(
        "ix_document_chunk_images_document_version_id",
        "document_chunk_images",
        ["document_version_id"],
        schema="rag",
    )
    op.add_column(
        "query_audit_events",
        sa.Column("multimodal_used", sa.Boolean(), nullable=False, server_default=sa.text("false")),
        schema="rag",
    )
    op.add_column(
        "query_audit_events",
        sa.Column("multimodal_image_count", sa.Integer(), nullable=False, server_default=sa.text("0")),
        schema="rag",
    )
    op.add_column(
        "query_audit_events",
        sa.Column("multimodal_image_detail", sa.Text(), nullable=True),
        schema="rag",
    )
    op.add_column(
        "query_audit_events",
        sa.Column("multimodal_image_bytes_total", sa.Integer(), nullable=False, server_default=sa.text("0")),
        schema="rag",
    )
    op.add_column(
        "query_audit_events",
        sa.Column("multimodal_image_ids", sa.JSON(), nullable=True),
        schema="rag",
    )


def downgrade() -> None:
    op.drop_column("query_audit_events", "multimodal_image_ids", schema="rag")
    op.drop_column("query_audit_events", "multimodal_image_bytes_total", schema="rag")
    op.drop_column("query_audit_events", "multimodal_image_detail", schema="rag")
    op.drop_column("query_audit_events", "multimodal_image_count", schema="rag")
    op.drop_column("query_audit_events", "multimodal_used", schema="rag")
    op.drop_index("ix_document_chunk_images_document_version_id", table_name="document_chunk_images", schema="rag")
    op.drop_index("ix_document_chunk_images_chunk_id", table_name="document_chunk_images", schema="rag")
    op.drop_table("document_chunk_images", schema="rag")
```

The revision id follows the existing timestamp style and uses the current Alembic head `20260522_134100` as its parent.

- [ ] **Step 4: Run migration tests and verify GREEN**

Run:

```powershell
Set-Location services\rag-api
uv run pytest tests/test_migrations.py -q
uv run ruff check .
Set-Location ..\..
```

Expected: migration tests pass and Ruff passes.

- [ ] **Step 5: Commit Task 1**

Run:

```powershell
git add services/rag-api/alembic/versions services/rag-api/tests/test_migrations.py
git commit -m "feat: add multimodal rag image schema"
```

Expected: commit succeeds with only RAG migration/test files.

## Task 2: Index Stable Image References Per Chunk

**Files:**
- Create: `services/rag-api/src/advanced_rag/rag/image_references.py`
- Modify: `services/rag-api/src/advanced_rag/rag/chunking.py`
- Modify: `services/rag-api/src/advanced_rag/rag/indexing_service.py`
- Modify: `services/rag-api/tests/test_chunking.py`
- Modify: `services/rag-api/tests/test_indexing.py`

- [ ] **Step 1: Write failing parser tests**

Add tests in `services/rag-api/tests/test_chunking.py`:

```python
def test_chunk_html_extracts_stable_document_image_references() -> None:
    html = """
    <h1>Panel</h1>
    <figure>
      <img
        src="/api/document-images/11111111-1111-1111-1111-111111111111/content"
        alt="Breaker panel photo"
      />
      <figcaption>North wall panel.</figcaption>
    </figure>
    """

    chunks = chunk_html(html)

    assert len(chunks) == 1
    assert chunks[0].image_references[0].image_id == UUID("11111111-1111-1111-1111-111111111111")
    assert chunks[0].image_references[0].ordinal == 0
    assert chunks[0].image_references[0].alt_text == "Breaker panel photo"
    assert chunks[0].image_references[0].caption == "North wall panel."


def test_chunk_html_ignores_non_document_image_urls() -> None:
    html = """
    <p>
      <img src="https://example.com/raw.png" alt="External image" />
      <img src="data:image/png;base64,AAAA" alt="Inline image" />
    </p>
    """

    chunks = chunk_html(html)

    assert chunks
    assert chunks[0].image_references == []
```

Add `from uuid import UUID` if it is not already imported.

- [ ] **Step 2: Run parser tests and verify RED**

Run:

```powershell
Set-Location services\rag-api
uv run pytest tests/test_chunking.py -q
Set-Location ..\..
```

Expected: failure because `DocumentChunk` does not expose `image_references`.

- [ ] **Step 3: Implement image reference value objects and parser**

Create `services/rag-api/src/advanced_rag/rag/image_references.py`:

```python
from __future__ import annotations

import re
from uuid import UUID

from pydantic import BaseModel, ConfigDict


DOCUMENT_IMAGE_URL_RE = re.compile(
    r"^/api/document-images/(?P<image_id>[0-9a-fA-F-]{36})/content$"
)


class ChunkImageReference(BaseModel):
    model_config = ConfigDict(frozen=True)

    image_id: UUID
    ordinal: int
    alt_text: str | None = None
    caption: str | None = None


def parse_document_image_url(src: str) -> UUID | None:
    match = DOCUMENT_IMAGE_URL_RE.match(src.strip())
    if match is None:
        return None
    try:
        return UUID(match.group("image_id"))
    except ValueError:
        return None
```

- [ ] **Step 4: Extend chunking to attach image references**

Modify `DocumentChunk` in `services/rag-api/src/advanced_rag/rag/chunking.py`:

```python
from advanced_rag.rag.image_references import ChunkImageReference, parse_document_image_url


class DocumentChunk(BaseModel):
    model_config = ConfigDict(frozen=True)

    chunk_index: int
    heading_path: list[str]
    token_count: int
    char_count: int
    content: str
    content_html: str
    image_references: list[ChunkImageReference] = []
```

Extend the HTML parser so `_Block` carries `image_references`, `_HtmlBlockParser.handle_starttag()` records valid document image URLs, and `_flush()` attaches buffered image references to the block. Preserve existing behavior that image alt/title/aria-label text remains indexable text and raw `src` values are not included in `content`.

- [ ] **Step 5: Run parser tests and verify GREEN**

Run:

```powershell
Set-Location services\rag-api
uv run pytest tests/test_chunking.py -q
Set-Location ..\..
```

Expected: all chunking tests pass.

- [ ] **Step 6: Write failing indexing persistence test**

Extend `services/rag-api/tests/test_indexing.py` with a payload containing a stable image URL and an assertion that `rag.document_chunk_images` persists one row for the job's chunk:

```python
def _indexing_payload_with_image() -> dict[str, str]:
    return {
        "documentId": str(uuid4()),
        "documentVersionId": str(uuid4()),
        "contentHtml": (
            "<h1>Safety panel</h1>"
            "<figure>"
            '<img src="/api/document-images/11111111-1111-1111-1111-111111111111/content" '
            'alt="Breaker panel with red emergency switch" />'
            "<figcaption>North wall panel.</figcaption>"
            "</figure>"
        ),
        "corpusMode": "published",
    }
```

Add an async reader:

```python
async def _read_chunk_images(dsn: str, job_id: UUID) -> list[dict[str, Any]]:
    connection = await asyncpg.connect(dsn)
    try:
        rows = await connection.fetch(
            """
            select image_id, ordinal, alt_text, caption
            from rag.document_chunk_images image
            join rag.document_chunks chunk on chunk.id = image.chunk_id
            where chunk.indexing_job_id = $1
            order by ordinal
            """,
            job_id,
        )
    finally:
        await connection.close()
    return [dict(row) for row in rows]
```

Test:

```python
def test_valid_indexing_request_persists_chunk_image_references() -> None:
    # Use the same container/bootstrap/app pattern as
    # test_valid_indexing_request_persists_job_chunks_and_embedding_dimensions.
    # Post the image payload, assert 200/Succeeded, then read chunk images.
    assert image_rows == [
        {
            "image_id": UUID("11111111-1111-1111-1111-111111111111"),
            "ordinal": 0,
            "alt_text": "Breaker panel with red emergency switch",
            "caption": "North wall panel.",
        }
    ]
```

- [ ] **Step 7: Run indexing test and verify RED**

Run:

```powershell
Set-Location services\rag-api
uv run pytest tests/test_indexing.py -q
Set-Location ..\..
```

Expected: failure because indexing does not write `rag.document_chunk_images`.

- [ ] **Step 8: Persist image references in indexing service**

In `services/rag-api/src/advanced_rag/rag/indexing_service.py`, after each chunk insert, keep the generated chunk id in a local variable and insert each `chunk.image_references` row into `rag.document_chunk_images`. Before inserting new rows for the version, delete stale rows by `document_version_id`.

Use this SQL shape:

```python
await session.execute(
    text("delete from rag.document_chunk_images where document_version_id = :document_version_id"),
    {"document_version_id": request.document_version_id},
)
```

For each chunk:

```python
chunk_id = uuid4()
```

Use `chunk_id` for both `rag.document_chunks.id` and `rag.document_chunk_images.chunk_id`.

For each image reference:

```python
await session.execute(
    text(
        """
        insert into rag.document_chunk_images (
            id,
            chunk_id,
            document_id,
            document_version_id,
            image_id,
            ordinal,
            alt_text,
            caption
        )
        values (
            :id,
            :chunk_id,
            :document_id,
            :document_version_id,
            :image_id,
            :ordinal,
            :alt_text,
            :caption
        )
        on conflict do nothing
        """
    ),
    {
        "id": uuid4(),
        "chunk_id": chunk_id,
        "document_id": request.document_id,
        "document_version_id": request.document_version_id,
        "image_id": image.image_id,
        "ordinal": image.ordinal,
        "alt_text": image.alt_text,
        "caption": image.caption,
    },
)
```

- [ ] **Step 9: Run focused RAG tests**

Run:

```powershell
Set-Location services\rag-api
uv run pytest tests/test_chunking.py tests/test_indexing.py tests/test_migrations.py -q
uv run ruff check .
Set-Location ..\..
```

Expected: tests pass and Ruff passes.

- [ ] **Step 10: Commit Task 2**

Run:

```powershell
git add services/rag-api/src/advanced_rag/rag/chunking.py services/rag-api/src/advanced_rag/rag/image_references.py services/rag-api/src/advanced_rag/rag/indexing_service.py services/rag-api/tests/test_chunking.py services/rag-api/tests/test_indexing.py
git commit -m "feat: index rag document image references"
```

Expected: commit succeeds with only RAG indexing/image reference files.

## Task 3: .NET Internal Document Image Content Endpoint

**Files:**
- Modify: `services/dotnet-api/src/AdvancedRag.Api/Controllers/DocumentImagesController.cs`
- Modify: `services/dotnet-api/src/AdvancedRag.App/DocumentImages/DocumentImageTypes.cs`
- Modify: `services/dotnet-api/tests/AdvancedRag.Api.Tests/DocumentImageEndpointTests.cs`

- [ ] **Step 1: Write failing API tests**

Add tests in `DocumentImageEndpointTests.cs`:

```csharp
[Fact]
public async Task InternalImageContent_RejectsMissingInternalToken()
{
    using WebApplicationFactory<Program> factory = CreateFactory();
    HttpClient client = factory.CreateClient();

    HttpResponseMessage response = await client.GetAsync(
        "/internal/document-images/11111111-1111-1111-1111-111111111111/content?userId=22222222-2222-2222-2222-222222222222&roles=Viewer");

    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    ErrorResponse? body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
    body!.Error.Code.Should().Be("AUTH_INTERNAL_TOKEN_INVALID");
}
```

Add success and forbidden tests using the existing image endpoint test fixture helpers. The success test must seed a user/document/image, send `X-Internal-Service-Token`, and assert `200`, image `Content-Type`, and returned bytes. The forbidden test must seed an image for a document the requested user cannot access and assert the shared envelope without leaking object keys.

- [ ] **Step 2: Run endpoint tests and verify RED**

Run:

```powershell
dotnet test services\dotnet-api\tests\AdvancedRag.Api.Tests\AdvancedRag.Api.Tests.csproj --filter DocumentImageEndpointTests
```

Expected: failure because the internal route does not exist.

- [ ] **Step 3: Add internal command types if needed**

If the public command shape is sufficient, reuse `GetDocumentImageContentCommand`. If tests need explicit internal semantics, add:

```csharp
public sealed record GetInternalDocumentImageContentCommand(
    Guid ImageId,
    Guid UserId,
    IReadOnlyList<string> Roles);
```

Keep service behavior centralized in `DocumentImageService.GetContentAsync`.

- [ ] **Step 4: Add internal route**

Add to `DocumentImagesController.cs`:

```csharp
[HttpGet("internal/document-images/{imageId:guid}/content")]
[AllowAnonymous]
public async Task<IActionResult> GetInternalContentAsync(
    Guid imageId,
    [FromQuery] Guid userId,
    [FromQuery] string[] roles,
    CancellationToken ct)
{
    if (!IsInternalTokenValid())
    {
        return Error(
            StatusCodes.Status401Unauthorized,
            "AUTH_INTERNAL_TOKEN_INVALID",
            "Internal service token is invalid.");
    }

    try
    {
        DocumentImageContentResult result = await _images.GetContentAsync(
            new GetDocumentImageContentCommand(imageId, userId, roles),
            ct);
        Response.Headers.ContentLength = result.SizeBytes;
        Response.Headers["X-Document-Image-Id"] = imageId.ToString("D");
        return File(result.Content, result.ContentType, enableRangeProcessing: false);
    }
    catch (DocumentImageException exception)
    {
        return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
    }
}
```

Add a private `IsInternalTokenValid()` helper matching `InternalSessionController` behavior: read `X-Internal-Service-Token`, read the expected token through `SecretConfiguration.Read`, compare with `CryptographicOperations.FixedTimeEquals`.

- [ ] **Step 5: Run .NET focused tests**

Run:

```powershell
dotnet test services\dotnet-api\tests\AdvancedRag.Api.Tests\AdvancedRag.Api.Tests.csproj --filter DocumentImageEndpointTests
dotnet build services\dotnet-api\AdvancedRag.sln --no-restore
```

Expected: tests and build pass.

- [ ] **Step 6: Commit Task 3**

Run:

```powershell
git add services/dotnet-api/src/AdvancedRag.Api/Controllers/DocumentImagesController.cs services/dotnet-api/src/AdvancedRag.App/DocumentImages/DocumentImageTypes.cs services/dotnet-api/tests/AdvancedRag.Api.Tests/DocumentImageEndpointTests.cs
git commit -m "feat: add internal document image content endpoint"
```

Expected: commit succeeds with only .NET image endpoint files.

## Task 4: FastAPI Internal Image Fetch And Selection Adapter

**Files:**
- Create: `services/rag-api/src/advanced_rag/rag/multimodal_images.py`
- Modify: `services/rag-api/src/advanced_rag/core/config.py`
- Modify: `services/rag-api/src/advanced_rag/main.py`
- Create: `services/rag-api/tests/test_multimodal_images.py`

- [ ] **Step 1: Write failing unit tests for image caps and ordering**

Create `services/rag-api/tests/test_multimodal_images.py` with tests for:

```python
def test_select_image_references_deduplicates_and_caps_by_order() -> None:
    # Given retrieved chunks [chunk_a, chunk_b] and references where the same image
    # appears twice, selection returns at most 3 unique image ids in retrieval order.
```

```python
async def test_fetch_selected_images_skips_failures_and_enforces_total_bytes() -> None:
    # Fake client returns two small images and one oversized image.
    # Result keeps only images within max_total_bytes.
```

Expected selected image model:

```python
class SelectedMultimodalImage(BaseModel):
    image_id: UUID
    content_type: str
    bytes_data: bytes
    byte_count: int
    detail: str
```

- [ ] **Step 2: Run tests and verify RED**

Run:

```powershell
Set-Location services\rag-api
uv run pytest tests/test_multimodal_images.py -q
Set-Location ..\..
```

Expected: import/module failures because the adapter does not exist.

- [ ] **Step 3: Add settings**

Add to `Settings` in `core/config.py`:

```python
multimodal_enabled: bool = True
multimodal_max_images: int = 3
multimodal_max_total_image_bytes: int = 5 * 1024 * 1024
multimodal_image_detail: str = "low"
dotnet_internal_image_base_url: str = "http://dotnet-api:8080/internal/document-images"
```

- [ ] **Step 4: Implement adapter**

Create `multimodal_images.py` with:

```python
from __future__ import annotations

import base64
from uuid import UUID

import httpx
from pydantic import BaseModel, ConfigDict
from sqlalchemy import text
from sqlalchemy.ext.asyncio import AsyncSession


ALLOWED_IMAGE_CONTENT_TYPES = {"image/png", "image/jpeg", "image/jpg", "image/webp", "image/gif"}


class ChunkImageCandidate(BaseModel):
    model_config = ConfigDict(frozen=True)

    chunk_id: UUID
    document_id: UUID
    document_version_id: UUID
    image_id: UUID
    ordinal: int


class SelectedMultimodalImage(BaseModel):
    model_config = ConfigDict(frozen=True)

    image_id: UUID
    content_type: str
    bytes_data: bytes
    byte_count: int
    detail: str

    def as_data_url(self) -> str:
        encoded = base64.b64encode(self.bytes_data).decode("ascii")
        return f"data:{self.content_type};base64,{encoded}"
```

Add functions:

```python
async def load_image_candidates(session: AsyncSession, chunk_ids: list[UUID]) -> list[ChunkImageCandidate]:
    if not chunk_ids:
        return []
    result = await session.execute(
        text(
            """
            select chunk_id, document_id, document_version_id, image_id, ordinal
            from rag.document_chunk_images
            where chunk_id = any(:chunk_ids)
            order by array_position(:chunk_ids, chunk_id), ordinal
            """
        ),
        {"chunk_ids": chunk_ids},
    )
    return [
        ChunkImageCandidate(
            chunk_id=row.chunk_id,
            document_id=row.document_id,
            document_version_id=row.document_version_id,
            image_id=row.image_id,
            ordinal=int(row.ordinal),
        )
        for row in result
    ]


def select_image_candidates(
    candidates: list[ChunkImageCandidate],
    *,
    max_images: int,
) -> list[ChunkImageCandidate]:
    selected: list[ChunkImageCandidate] = []
    seen: set[UUID] = set()
    for candidate in candidates:
        if candidate.image_id in seen:
            continue
        selected.append(candidate)
        seen.add(candidate.image_id)
        if len(selected) >= max_images:
            break
    return selected


async def fetch_selected_images(
    candidates: list[ChunkImageCandidate],
    *,
    user_id: UUID,
    roles: list[str],
    internal_token: str,
    base_url: str,
    max_total_bytes: int,
    detail: str,
    client: httpx.AsyncClient | None = None,
) -> list[SelectedMultimodalImage]:
    owns_client = client is None
    http = client or httpx.AsyncClient(timeout=10)
    selected: list[SelectedMultimodalImage] = []
    total_bytes = 0
    try:
        for candidate in candidates:
            url = f"{base_url.rstrip('/')}/{candidate.image_id}/content"
            params = [("userId", str(user_id)), *[("roles", role) for role in roles]]
            response = await http.get(
                url,
                params=params,
                headers={"X-Internal-Service-Token": internal_token},
            )
            if response.status_code != 200:
                continue
            content_type = response.headers.get("content-type", "").split(";", 1)[0].lower()
            if content_type not in ALLOWED_IMAGE_CONTENT_TYPES:
                continue
            byte_count = len(response.content)
            if total_bytes + byte_count > max_total_bytes:
                continue
            selected.append(
                SelectedMultimodalImage(
                    image_id=candidate.image_id,
                    content_type=content_type,
                    bytes_data=response.content,
                    byte_count=byte_count,
                    detail=detail,
                )
            )
            total_bytes += byte_count
    finally:
        if owns_client:
            await http.aclose()
    return selected
```

Use `.NET` URL:

```python
url = f"{base_url.rstrip('/')}/{candidate.image_id}/content"
params = [("userId", str(user_id)), *[("roles", role) for role in roles]]
headers = {"X-Internal-Service-Token": internal_token}
```

Skip responses with non-200 status, unsupported content types, or byte size that would exceed the total cap.

- [ ] **Step 5: Run adapter tests**

Run:

```powershell
Set-Location services\rag-api
uv run pytest tests/test_multimodal_images.py -q
uv run ruff check .
Set-Location ..\..
```

Expected: tests pass and Ruff passes.

- [ ] **Step 6: Commit Task 4**

Run:

```powershell
git add services/rag-api/src/advanced_rag/core/config.py services/rag-api/src/advanced_rag/rag/multimodal_images.py services/rag-api/tests/test_multimodal_images.py
git commit -m "feat: select authorized multimodal rag images"
```

Expected: commit succeeds with only FastAPI adapter/config/test files.

## Task 5: OpenAI Responses Multimodal Provider Path

**Files:**
- Modify: `services/rag-api/src/advanced_rag/providers/base.py`
- Modify: `services/rag-api/src/advanced_rag/providers/openai_provider.py`
- Modify: `services/rag-api/src/advanced_rag/rag/answer_generator.py`
- Create: `services/rag-api/tests/test_openai_responses_multimodal.py`

- [ ] **Step 1: Write failing provider payload test**

Create test that injects a fake OpenAI client into `OpenAIProvider`, calls a new `responses_complete()` or `multimodal_complete()` method, and asserts the captured payload contains:

```python
{
    "store": False,
    "input": [
        {
            "role": "user",
            "content": [
                {"type": "input_text", "text": "Context:\n[chunk_id=11111111-1111-1111-1111-111111111111]\nVisual safety context\n\nQuestion:\nWhat color is the emergency switch?"},
                {
                    "type": "input_image",
                    "image_url": "data:image/png;base64,iVBORw0KGgo=",
                    "detail": "low",
                },
            ],
        }
    ],
    "text": {
        "format": {
            "type": "json_schema",
            "name": "AssistantAnswer",
            "strict": True,
            "schema": {
                "type": "object",
                "additionalProperties": False,
                "required": ["answer", "cited_chunk_ids"],
                "properties": {
                    "answer": {"type": "string"},
                    "cited_chunk_ids": {
                        "type": "array",
                        "items": {"type": "string"},
                    },
                },
            },
        }
    },
}
```

- [ ] **Step 2: Run provider test and verify RED**

Run:

```powershell
Set-Location services\rag-api
uv run pytest tests/test_openai_responses_multimodal.py -q
Set-Location ..\..
```

Expected: failure because multimodal provider methods do not exist.

- [ ] **Step 3: Add provider request models**

In `providers/base.py`, add:

```python
class ImageInput(BaseModel):
    model_config = ConfigDict(frozen=True)

    image_url: str
    detail: Literal["low", "high", "auto"] = "low"


class MultimodalCompletionRequest(BaseModel):
    model_config = ConfigDict(frozen=True)

    instructions: str
    text: str
    images: list[ImageInput]
    model: str
    temperature: float = 0.1
    max_tokens: int = 900
    response_format: dict


class ILlmProvider(Protocol):
    async def multimodal_complete(
        self, req: MultimodalCompletionRequest
    ) -> tuple[str, ChatUsage]:
        """Non-streaming multimodal completion. Returns full JSON content and usage."""
```

- [ ] **Step 4: Implement OpenAI Responses call**

In `openai_provider.py`, implement `multimodal_complete` using `self._client.responses.create(request_payload)`. Map usage fields into `ChatUsage`. Use `response.output_text` when available; otherwise walk output message content text.

- [ ] **Step 5: Add answer generator function**

In `answer_generator.py`, add:

```python
async def generate_multimodal_answer(
    *,
    llm: ILlmProvider,
    question: str,
    chunks: list[Any],
    image_inputs: list[ImageInput],
    locale: str,
    model: str,
    temperature: float = 0.1,
    max_tokens: int = 900,
) -> AnswerGeneration:
    system = load_system_prompt(locale)
    user_text = (
        f"Context:\n{_format_context(chunks)}\n\n"
        f"Question:\n{question}\n\n"
        f"{JSON_INSTRUCTION}"
    )
    req = MultimodalCompletionRequest(
        instructions=system,
        text=user_text,
        images=image_inputs,
        model=model,
        temperature=temperature,
        max_tokens=max_tokens,
        response_format={
            "type": "json_schema",
            "name": "AssistantAnswer",
            "strict": True,
            "schema": {
                "type": "object",
                "additionalProperties": False,
                "required": ["answer", "cited_chunk_ids"],
                "properties": {
                    "answer": {"type": "string"},
                    "cited_chunk_ids": {
                        "type": "array",
                        "items": {"type": "string"},
                    },
                },
            },
        },
    )
    content, usage = await llm.multimodal_complete(req)
    answer, cited = _parse_answer(content)
    return AnswerGeneration(answer=answer, cited_chunk_ids=cited, usage=usage)
```

Use the same JSON parsing contract as `generate_answer`, but pass system prompt as `instructions` and combined context/question as `text`.

- [ ] **Step 6: Run provider/generator tests**

Run:

```powershell
Set-Location services\rag-api
uv run pytest tests/test_openai_responses_multimodal.py tests/test_chat_rag.py -q
uv run ruff check .
uv run mypy src tests
Set-Location ..\..
```

Expected: tests, Ruff, and mypy pass.

- [ ] **Step 7: Commit Task 5**

Run:

```powershell
git add services/rag-api/src/advanced_rag/providers/base.py services/rag-api/src/advanced_rag/providers/openai_provider.py services/rag-api/src/advanced_rag/rag/answer_generator.py services/rag-api/tests/test_openai_responses_multimodal.py services/rag-api/tests/test_chat_rag.py
git commit -m "feat: add openai responses multimodal provider"
```

Expected: commit succeeds with provider/generator/test files.

## Task 6: Chat Service Integration, Audit, And No-Cache Rule

**Files:**
- Modify: `services/rag-api/src/advanced_rag/rag/chat_service.py`
- Modify: `services/rag-api/tests/test_chat_rag.py`

- [ ] **Step 1: Write failing chat service tests**

Add tests in `test_chat_rag.py` that prove:

```python
def test_chat_uses_multimodal_generation_when_retrieved_chunks_have_images() -> None:
    # Seed chunks, image references, fake .NET image fetch, fake LLM.
    # Assert answer uses multimodal path and audit row has multimodal_used=true.
```

```python
def test_multimodal_answer_is_not_written_to_semantic_cache() -> None:
    # Same setup, then assert rag.semantic_cache_entries remains empty.
```

```python
def test_chat_falls_back_to_text_when_image_fetch_returns_no_images() -> None:
    # Image candidates exist but fetch adapter skips all.
    # Assert text-only LLM path is used and multimodal_used=false.
```

- [ ] **Step 2: Run chat tests and verify RED**

Run:

```powershell
Set-Location services\rag-api
uv run pytest tests/test_chat_rag.py -q
Set-Location ..\..
```

Expected: failures because chat service does not select images or write multimodal audit fields.

- [ ] **Step 3: Integrate image selection**

In `ChatService.answer()` after `_retrieve_chunks()` and before generation:

```python
selected_images: list[SelectedMultimodalImage] = []
if chunks and self._settings.multimodal_enabled:
    candidates = await load_image_candidates(session, [chunk.id for chunk in chunks])
    selected_candidates = select_image_candidates(
        candidates,
        max_images=self._settings.multimodal_max_images,
    )
    selected_images = await fetch_selected_images(
        selected_candidates,
        user_id=UUID(claims.user_id),
        roles=[claims.role],
        internal_token=self._settings.resolved_internal_service_token,
        base_url=self._settings.dotnet_internal_image_base_url,
        max_total_bytes=self._settings.multimodal_max_total_image_bytes,
        detail=self._settings.multimodal_image_detail,
    )
```

- [ ] **Step 4: Route generation by selected image count**

Use `generate_multimodal_answer()` when `selected_images` is not empty. Otherwise keep existing `generate_answer()`.

- [ ] **Step 5: Extend audit insert**

Add parameters to `_insert_audit()`:

```python
multimodal_used: bool
multimodal_image_count: int
multimodal_image_detail: str | None
multimodal_image_bytes_total: int
multimodal_image_ids: list[UUID] | None
```

Write them into `rag.query_audit_events`.

- [ ] **Step 6: Enforce no-cache for multimodal answers**

Change cache write condition:

```python
if citations and not selected_images:
    await self._write_cache(
        session,
        audit_id=audit_id,
        corpus=corpus,
        access_scope_hash=claims.access_scope_hash,
        filters_hash=filters_hash,
        question=normalized_question,
        answer=completion.answer,
        question_embedding=question_embedding,
        citations=citations,
    )
```

- [ ] **Step 7: Run focused RAG suite**

Run:

```powershell
Set-Location services\rag-api
uv run pytest tests/test_chat_rag.py tests/test_multimodal_images.py tests/test_openai_responses_multimodal.py -q
uv run ruff check .
uv run mypy src tests
Set-Location ..\..
```

Expected: tests, Ruff, and mypy pass.

- [ ] **Step 8: Commit Task 6**

Run:

```powershell
git add services/rag-api/src/advanced_rag/rag/chat_service.py services/rag-api/tests/test_chat_rag.py
git commit -m "feat: use multimodal images in rag chat"
```

Expected: commit succeeds with chat integration files.

## Task 7: Full Verification And Context Update

**Files:**
- Modify: `context/progress-tracker.md`
- Modify only if implementation changed decisions: `context/design-decisions.md`

- [ ] **Step 1: Run full backend verification**

Run:

```powershell
dotnet test services\dotnet-api\AdvancedRag.sln --filter "DocumentImage|Health|Auth"
dotnet build services\dotnet-api\AdvancedRag.sln --no-restore
Set-Location services\rag-api
uv run pytest -q
uv run ruff check .
uv run mypy src tests
Set-Location ..\..
git diff --check
```

Expected:

- Focused .NET tests pass.
- .NET build passes.
- Full RAG pytest suite passes.
- Ruff passes.
- mypy passes.
- `git diff --check` has no errors; line-ending warnings are acceptable if they match existing repository behavior.

- [ ] **Step 2: Update progress tracker**

Append an English completed-work entry to `context/progress-tracker.md`:

```markdown
- On 2026-06-01, query-time multimodal RAG was implemented. FastAPI persists `chunk -> image_id` references during indexing, `.NET` exposes an internal token-protected image content endpoint, FastAPI selects and fetches a capped set of authorized images from final retrieved chunks, OpenAI Responses API handles multimodal generation, and multimodal answers are audited without semantic-cache writes. Verification passed with the commands listed in this session.
```

Update `In Progress`, `Next Up`, and `Handoff For Next Session` to reflect the user-owned Compose/browser acceptance checkpoint.

- [ ] **Step 3: Update design decisions only if needed**

If implementation stays within the approved design, do not add a new `design-decisions.md` entry. If implementation changes API choice, caps, cache behavior, audit fields, provider model assumptions, or service ownership, add a new dated decision entry.

- [ ] **Step 4: Commit verification/context**

Run:

```powershell
git add context/progress-tracker.md context/design-decisions.md
git commit -m "docs: record multimodal rag implementation"
```

Expected: commit succeeds if context files changed. If only `progress-tracker.md` changed, stage only that file.

## Task 8: User-Owned Local Acceptance

**Files:** none agent-owned unless acceptance exposes a defect.

- [ ] **Step 1: User starts local stack**

User runs:

```powershell
.\infra\compose\Start-Local.ps1 -TrustCaddyCertificate
docker compose --env-file infra\compose\.env.example -f infra\compose\compose.yaml -f infra\compose\compose.override.yaml ps -a
```

Expected: `dotnet-api`, `rag-api`, `postgres`, `minio`, `minio-init`, Caddy, and frontends are healthy or completed successfully.

- [ ] **Step 2: User creates visual-evidence document**

User opens `https://manage.localhost`, creates or edits a document, uploads an image whose visual content is not repeated in surrounding text, saves, sends to review, and publishes.

Expected: publication succeeds and indexing status is successful.

- [ ] **Step 3: User asks visual chat question**

User opens `https://chat.localhost` and asks a question answerable only from the image.

Expected: chat answers in Spanish and cites the document/chunk.

- [ ] **Step 4: User verifies audit**

User or agent runs a read-only SQL check:

```powershell
docker compose --env-file infra\compose\.env.example -f infra\compose\compose.yaml -f infra\compose\compose.override.yaml exec postgres psql -U postgres -d advanced_rag -c "select id, multimodal_used, multimodal_image_count, multimodal_image_detail, multimodal_image_bytes_total from rag.query_audit_events order by created_at desc limit 5;"
```

Expected: the latest visual question row has `multimodal_used = true`, `multimodal_image_count > 0`, `multimodal_image_detail = low`, and `multimodal_image_bytes_total > 0`.

- [ ] **Step 5: Record acceptance result**

If acceptance passes, update `context/progress-tracker.md` with the user-owned result and commit:

```powershell
git add context/progress-tracker.md
git commit -m "docs: record multimodal rag local acceptance"
```

Expected: commit succeeds.
