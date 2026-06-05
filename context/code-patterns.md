# Code Patterns

Concrete reference patterns. Copy and adapt; do not invent new shapes. If a pattern below conflicts with `architecture.md` or `code-standards.md`, those win and this file must be updated.

## Shared Error Envelope

The same shape is implemented in three stacks. Field names are stable.

### TypeScript (frontends)

```ts
// apps/<app>/src/api/errors.ts
export interface ApiErrorEnvelope {
  error: {
    code: string;
    message: string;
    details?: Record<string, unknown> | null;
    requestId: string;
  };
}

export class ApiError extends Error {
  constructor(
    public readonly code: string,
    public readonly httpStatus: number,
    public readonly requestId: string,
    public readonly details: Record<string, unknown> | null,
    message: string,
  ) {
    super(message);
  }
}
```

### .NET (`services/dotnet-api`)

```csharp
// App/Errors/ApiErrorEnvelope.cs
public sealed record ApiErrorBody(
    string Code,
    string Message,
    object? Details,
    string RequestId);

public sealed record ApiErrorEnvelope(ApiErrorBody Error);

// App/Errors/ApiException.cs
public sealed class ApiException : Exception
{
    public string Code { get; }
    public int HttpStatus { get; }
    public object? Details { get; }

    public ApiException(string code, int httpStatus, string message, object? details = null)
        : base(message)
    {
        Code = code;
        HttpStatus = httpStatus;
        Details = details;
    }
}
```

### FastAPI (`services/rag-api`)

```python
# src/advanced_rag/core/errors.py
from typing import Any
from pydantic import BaseModel

class ApiErrorBody(BaseModel):
    code: str
    message: str
    details: dict[str, Any] | None = None
    request_id: str  # serialized as "requestId" via Pydantic alias config

class ApiErrorEnvelope(BaseModel):
    error: ApiErrorBody

class ApiException(Exception):
    def __init__(
        self,
        code: str,
        http_status: int,
        message: str,
        details: dict[str, Any] | None = None,
    ) -> None:
        super().__init__(message)
        self.code = code
        self.http_status = http_status
        self.details = details
```

Both backends register a global exception handler that converts `ApiException` to the envelope shape with the current request's id. Unexpected exceptions become `{"code": "INTERNAL_ERROR", "message": "An internal error occurred."}` with a 500 status; the technical detail is logged, never returned.

## Stable Error Codes (Initial Catalog)

Codes are stable strings, UPPER_SNAKE_CASE. Document new codes in this file when introduced.

| Code | HTTP | Meaning |
| --- | --- | --- |
| `VALIDATION_FAILED` | 400 | Request body or query parameters failed validation. `details` contains field errors. |
| `AUTH_REQUIRED` | 401 | Missing or invalid session/token. |
| `AUTH_TOKEN_INVALID` | 401 | Token signature or required claims are invalid. |
| `AUTH_TOKEN_EXPIRED` | 401 | Token signature valid but expired. |
| `AUTH_TOKEN_INVALID_KEY` | 401 | Token signed by a retired key id. |
| `AUTH_INTERNAL_TOKEN_INVALID` | 401 | Missing or wrong internal service token on internal endpoints. |
| `AUTH_FORBIDDEN` | 403 | Authenticated but not authorized for this resource. |
| `CSRF_TOKEN_INVALID` | 400 | Missing or invalid CSRF cookie/header pair on a mutating browser request. |
| `NOT_FOUND` | 404 | Resource does not exist or is not visible to the caller. |
| `CONFLICT` | 409 | Concurrent modification or invalid state transition. |
| `SETUP_ALREADY_COMPLETED` | 409 | First-run admin setup is permanently closed because an Admin already exists. |
| `INVALID_LIFECYCLE_TRANSITION` | 409 | Document lifecycle transition is not allowed in the current state. |
| `RATE_LIMITED` | 429 | Generic rate-limit trip. |
| `LOGIN_IP_RATE_LIMITED` | 429 | Login attempts from one origin IP exceeded the configured technical limit. |
| `LOGIN_USER_RATE_LIMITED` | 429 | Login attempts for one user/email exceeded the configured technical limit. |
| `CHAT_RATE_LIMITED` | 429 | Chat questions for one user exceeded the configured technical limit. |
| `IMPORT_RATE_LIMITED` | 429 | Assisted import extraction requests for one user exceeded the configured technical limit. |
| `VIEWER_EXCHANGE_RATE_LIMITED` | 429 | Viewer exchange attempts exceeded the configured technical limit. |
| `AI_BUDGET_EXCEEDED` | 429 | User reached configured monthly AI budget. |
| `IMPORT_TEXT_NOT_EXTRACTABLE` | 422 | Uploaded PDF/DOCX has no extractable text. |
| `IMPORT_FILE_TOO_LARGE` | 413 | Uploaded file exceeds the configured size limit. |
| `DOCUMENT_IMAGE_SOURCE_INVALID` | 400 | Document HTML contains an image source that is not a stable app-controlled URL. |
| `DOCUMENT_IMAGE_TOO_LARGE` | 413 | Uploaded document image exceeds the configured size limit. |
| `DOCUMENT_IMAGE_TYPE_UNSUPPORTED` | 415 | Uploaded document image MIME type is not supported. |
| `INDEXING_FAILED` | 502 | Pre-publication indexing failed; safe summary returned. |
| `INDEXING_NO_CONTENT` | 422 | Content produced zero indexable chunks. |
| `RAG_PROVIDER_UNAVAILABLE` | 503 | OpenAI returned a retryable error after exhausting retries. |
| `RAG_PROVIDER_MISCONFIGURED` | 500 | OpenAI configuration is invalid (model id, auth, etc.). |
| `SESSION_HANDOFF_INVALID` | 401 | Cross-app session handoff code is missing, unknown, or for a different target surface. |
| `SESSION_HANDOFF_EXPIRED` | 410 | Cross-app session handoff code expired before consumption. |
| `SESSION_HANDOFF_USED` | 410 | Cross-app session handoff code was already consumed. |
| `VIEWER_CODE_EXPIRED` | 410 | Viewer exchange code expired. |
| `VIEWER_CODE_USED` | 410 | Viewer exchange code already consumed. |
| `VIEWER_CODE_INVALID` | 400 | Viewer exchange code malformed or unknown. |
| `INTERNAL_ERROR` | 500 | Catch-all for unexpected errors. |

## .NET Thin Controller + Service

```csharp
// Api/Controllers/DocumentsController.cs
[ApiController]
[Route("api/documents")]
public sealed class DocumentsController : ControllerBase
{
    private readonly IDocumentLifecycleService _lifecycle;

    public DocumentsController(IDocumentLifecycleService lifecycle) => _lifecycle = lifecycle;

    [HttpPost("{id:guid}/send-to-review")]
    [Authorize(Roles = "Admin,DocumentManager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendToReview(
        Guid id,
        [FromBody] SendToReviewRequest body,
        CancellationToken ct)
    {
        LifecycleTransitionResult result = await _lifecycle.SendToReviewAsync(id, body.Comment, User.GetUserId(), ct);
        return Ok(new SendToReviewResponse(result.NewState, result.VersionNumber));
    }
}

// App/Documents/IDocumentLifecycleService.cs
public interface IDocumentLifecycleService
{
    Task<LifecycleTransitionResult> SendToReviewAsync(
        Guid documentId,
        string? comment,
        Guid actorUserId,
        CancellationToken ct);
}

// App/Documents/DocumentLifecycleService.cs
public sealed class DocumentLifecycleService : IDocumentLifecycleService
{
    private readonly AppDbContext _db;
    private readonly IAuditWriter _audit;
    private readonly ILogger<DocumentLifecycleService> _log;

    public DocumentLifecycleService(AppDbContext db, IAuditWriter audit, ILogger<DocumentLifecycleService> log)
    {
        _db = db;
        _audit = audit;
        _log = log;
    }

    public async Task<LifecycleTransitionResult> SendToReviewAsync(
        Guid documentId,
        string? comment,
        Guid actorUserId,
        CancellationToken ct)
    {
        Document document = await _db.Documents
            .Include(i => i.CurrentDraftVersion)
            .FirstOrDefaultAsync(i => i.Id == documentId, ct)
            ?? throw new ApiException("NOT_FOUND", 404, "Document not found.");

        if (document.CurrentDraftVersion is null || document.CurrentDraftVersion.State != VersionState.Draft)
            throw new ApiException("INVALID_LIFECYCLE_TRANSITION", 409, "No draft to send to review.");

        document.CurrentDraftVersion.RequireFieldsForReview();
        document.CurrentDraftVersion.State = VersionState.InReview;
        document.CurrentDraftVersion.SubmittedForReviewAt = DateTimeOffset.UtcNow;
        document.CurrentDraftVersion.SubmittedForReviewBy = actorUserId;

        if (!string.IsNullOrWhiteSpace(comment))
            _db.ReviewComments.Add(new ReviewComment(document.CurrentDraftVersion.Id, actorUserId, comment));

        await _audit.WriteAsync(new AuditEvent(
            actorUserId,
            "document.send_to_review",
            new { documentId, versionId = document.CurrentDraftVersion.Id }), ct);

        await _db.SaveChangesAsync(ct);
        return new LifecycleTransitionResult(VersionState.InReview, document.CurrentDraftVersion.VersionNumber);
    }
}
```

## FastAPI Endpoint With Auth + Audit Dependencies

```python
# src/advanced_rag/api/chat.py
from fastapi import APIRouter, Depends, Request
from advanced_rag.api.dependencies import current_user, request_id, audit_writer
from advanced_rag.core.errors import ApiException
from advanced_rag.rag.chat_service import ChatService, ChatService_Dep
from advanced_rag.schemas.chat import ChatRequest

router = APIRouter(prefix="/api/chat", tags=["chat"])

@router.post("")
async def post_chat(
    body: ChatRequest,
    request: Request,
    user = Depends(current_user),
    rid: str = Depends(request_id),
    audit = Depends(audit_writer),
    chat: ChatService = Depends(ChatService_Dep),
):
    if not body.question.strip():
        raise ApiException("VALIDATION_FAILED", 400, "Question is required.",
                           details={"field": "question"})
    return await chat.answer_streaming(
        user=user,
        question=body.question,
        request_id=rid,
        audit=audit,
    )
```

```python
# src/advanced_rag/api/dependencies.py
from fastapi import Depends, Request
from advanced_rag.auth.session_validation import SessionValidatorProtocol
from advanced_rag.audit.writer import QueryAuditWriter
from advanced_rag.core.errors import ApiException

async def request_id(request: Request) -> str:
    return request.state.request_id  # populated by asgi-correlation-id middleware

async def current_user(request: Request, rid: str = Depends(request_id)):
    cookie = request.cookies.get(request.app.state.settings.session_cookie_name)
    if cookie is None:
        raise ApiException("AUTH_REQUIRED", 401, "Session required.")
    validator: SessionValidatorProtocol = request.app.state.session_validator
    return await validator.validate(cookie, request_id=rid)

def audit_writer(request: Request) -> QueryAuditWriter:
    return request.app.state.audit_writer
```

## EF Core Migration

```csharp
// Infrastructure/Migrations/20260518_AddDocumentPermissions.cs
public partial class AddDocumentPermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "document_permissions",
            schema: "app",
            columns: table => new
            {
                id = table.Column<Guid>(nullable: false),
                document_id = table.Column<Guid>(nullable: false),
                group_id = table.Column<Guid>(nullable: true),
                attribute_key = table.Column<string>(maxLength: 64, nullable: true),
                attribute_value = table.Column<string>(maxLength: 256, nullable: true),
                created_at = table.Column<DateTimeOffset>(nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_document_permissions", x => x.id);
                table.ForeignKey(
                    name: "FK_document_permissions_documents_document_id",
                    column: x => x.document_id,
                    principalSchema: "app",
                    principalTable: "documents",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_document_permissions_document_id",
            schema: "app",
            table: "document_permissions",
            column: "document_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable(name: "document_permissions", schema: "app");
}
```

## Alembic Migration With pgvector And Grants

```python
# services/rag-api/alembic/versions/20260518_add_document_chunks.py
"""add document_chunks with pgvector and HNSW index"""
from alembic import op
import sqlalchemy as sa
from pgvector.sqlalchemy import Vector

revision = "20260518_chunks"
down_revision = "20260517_init_rag"

def upgrade() -> None:
    op.create_table(
        "document_chunks",
        sa.Column("id", sa.Uuid(), primary_key=True),
        sa.Column("document_id", sa.Uuid(), nullable=False),
        sa.Column("document_version_id", sa.Uuid(), nullable=False),
        sa.Column("chunk_index", sa.Integer(), nullable=False),
        sa.Column("heading_path", sa.ARRAY(sa.Text()), nullable=False, server_default="{}"),
        sa.Column("content", sa.Text(), nullable=False),
        sa.Column("content_html", sa.Text(), nullable=False),
        sa.Column("token_count", sa.Integer(), nullable=False),
        sa.Column("char_count", sa.Integer(), nullable=False),
        sa.Column("embedding", Vector(1024), nullable=False),
        sa.Column("embedding_model", sa.Text(), nullable=False),
        sa.Column("is_active", sa.Boolean(), nullable=False, server_default=sa.text("true")),
        sa.Column("created_at", sa.DateTime(timezone=True), server_default=sa.text("now()")),
        schema="rag",
    )
    op.create_index(
        "ix_document_chunks_document_version_id",
        "document_chunks",
        ["document_version_id"],
        schema="rag",
    )
    op.execute(
        "CREATE INDEX ix_document_chunks_embedding_hnsw "
        "ON rag.document_chunks USING hnsw (embedding vector_cosine_ops) "
        "WITH (m = 16, ef_construction = 64)"
    )

def downgrade() -> None:
    op.drop_index("ix_document_chunks_embedding_hnsw", table_name="document_chunks", schema="rag")
    op.drop_index("ix_document_chunks_document_version_id", table_name="document_chunks", schema="rag")
    op.drop_table("document_chunks", schema="rag")
```

## xUnit Integration Test

```csharp
// services/dotnet-api/tests/Api.IntegrationTests/DocumentLifecycleTests.cs
public sealed class DocumentLifecycleTests : IClassFixture<PostgresWebApplicationFactory>
{
    private readonly PostgresWebApplicationFactory _factory;

    public DocumentLifecycleTests(PostgresWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task SendToReview_FromDraft_TransitionsToInReview()
    {
        HttpClient client = _factory.CreateClient();
        await client.AuthenticateAsAsync(role: "DocumentManager");
        Guid draftId = await client.SeedDraftDocumentAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/documents/{draftId}/send-to-review",
            new { Comment = "ready for review" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        SendToReviewResponse? body = await response.Content.ReadFromJsonAsync<SendToReviewResponse>();
        body!.NewState.Should().Be("InReview");

        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.AuditEvents.Where(a => a.EventType == "document.send_to_review").CountAsync())
            .Should().Be(1);
    }
}
```

## pytest Integration Test

```python
# services/rag-api/tests/test_chat_endpoint.py
import pytest
from httpx import AsyncClient

@pytest.mark.asyncio
async def test_chat_rejects_empty_question(app_client: AsyncClient, viewer_session):
    response = await app_client.post(
        "/api/chat",
        json={"question": "   "},
        cookies=viewer_session.cookies,
    )
    assert response.status_code == 400
    body = response.json()
    assert body["error"]["code"] == "VALIDATION_FAILED"
    assert body["error"]["details"] == {"field": "question"}
    assert "requestId" in body["error"]

@pytest.mark.asyncio
async def test_chat_streams_sse(app_client: AsyncClient, viewer_session, seeded_document):
    async with app_client.stream(
        "POST",
        "/api/chat",
        json={"question": "How do I onboard?"},
        cookies=viewer_session.cookies,
    ) as resp:
        assert resp.status_code == 200
        events = [chunk async for chunk in resp.aiter_text()]
        joined = "".join(events)
        assert "event: request-id" in joined
        assert "event: done" in joined
```

## Vitest Component Test

```ts
// apps/chat-web/src/components/__tests__/FeedbackButtons.test.tsx
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, it, expect, vi } from "vitest";
import { FeedbackButtons } from "../FeedbackButtons";

describe("FeedbackButtons", () => {
  it("shows comment field after thumbs down", async () => {
    const onSubmit = vi.fn();
    render(<FeedbackButtons answerId="a1" onSubmit={onSubmit} />);

    await userEvent.click(screen.getByRole("button", { name: /no me sirvió/i }));

    expect(screen.getByRole("textbox", { name: /comentario opcional/i })).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: /enviar/i }));

    expect(onSubmit).toHaveBeenCalledWith({
      answerId: "a1",
      value: "down",
      comment: "",
    });
  });
});
```

## Caddyfile (Compose Production)

```caddyfile
# infra/compose/Caddyfile
{
    email {$LETSENCRYPT_EMAIL}
    auto_https disable_redirects
    log {
        output file /var/log/caddy/access.json
        format json
    }
}

(security_headers) {
    header {
        Strict-Transport-Security "max-age=31536000; includeSubDomains"
        X-Content-Type-Options "nosniff"
        Referrer-Policy "no-referrer"
        X-Frame-Options "DENY"
        Permissions-Policy "geolocation=(), microphone=(), camera=()"
    }
}

manage.{$PUBLIC_DOMAIN} {
    import security_headers
    @api path /api/*
    handle @api {
        reverse_proxy dotnet-api:8080 {
            header_up X-Request-ID {http.request.header.X-Request-ID}
            header_up X-Request-ID {http.request.uuid}
        }
    }
    handle {
        root * /srv/manage-web
        try_files {path} /index.html
        file_server
    }
}

chat.{$PUBLIC_DOMAIN} {
    import security_headers
    @rag path /api/chat/* /api/feedback/* /api/chat
    handle @rag {
        reverse_proxy rag-api:8000 {
            flush_interval -1
            header_up X-Request-ID {http.request.header.X-Request-ID}
            header_up X-Request-ID {http.request.uuid}
        }
    }
    @netauth path /api/auth/* /api/session/* /api/csrf /api/viewer/*
    handle @netauth {
        reverse_proxy dotnet-api:8080
    }
    handle {
        root * /srv/chat-web
        try_files {path} /index.html
        file_server
    }
}

docs.{$PUBLIC_DOMAIN} {
    import security_headers
    @api path /api/*
    handle @api {
        reverse_proxy dotnet-api:8080
    }
    handle {
        root * /srv/docs-web
        try_files {path} /index.html
        file_server
    }
}
```

For local development, replace `{$PUBLIC_DOMAIN}` with `localhost` and rely on Caddy's internal CA.

## Docker Compose Baseline

The current Compose baseline lives in `infra/compose/compose.yaml`; copy from the implemented file instead of recreating the skeleton from memory.

Key rules:

- Include `postgres`, `postgres-init`, `.NET`, FastAPI, the three frontends, and Caddy.
- Gate `.NET` and FastAPI on `postgres-init` with `condition: service_completed_successfully`.
- Use separate Compose secrets for `postgres_admin_password`, `postgres_app_password`, `postgres_rag_password`, and `postgres_reporting_password`.
- Pass database password file paths to services. Do not put database passwords directly in environment variables.
- Use `jwt_signing_keys.json`, `csrf_signing_key.txt`, `openai_api_key.txt`, and `internal_service_token.txt` as file-mounted secrets.
- Preserve `/api/*` prefixes in Caddy reverse proxy routes. Do not use `handle_path` for backend API routes.

## .env.example

```dotenv
# infra/compose/.env.example â€” non-sensitive runtime configuration only.
# Sensitive values (passwords, API keys) live in ./secrets/* mounted as Compose secrets.

PUBLIC_DOMAIN=localhost
LETSENCRYPT_EMAIL=admin@example.com

POSTGRES_DB=advanced_rag
POSTGRES_SUPERUSER=postgres

OPENAI_CHAT_MODEL=gpt-4.1-nano
OPENAI_EMBEDDING_MODEL=text-embedding-3-small
OPENAI_EMBEDDING_DIMENSIONS=1536

RAG_SEMANTIC_CACHE_TTL_HOURS=24
RAG_SEMANTIC_CACHE_SIMILARITY_THRESHOLD=0.90

LOG_RETENTION_DAYS=30
POSTGRES_BACKUP_RETENTION_DAYS=14

CUSTOMER_TIMEZONE=America/Argentina/Buenos_Aires
DEFAULT_MONTHLY_AI_BUDGET_USD=5
```

## postgres-init

The current Postgres initialization scripts live in:

- `infra/compose/postgres-init/init.sh`
- `infra/compose/postgres-init/init.sql`

The scripts must stay idempotent. They create the database if missing, enable `pgvector`, create or update service role passwords from Compose secrets, create the `app` and `rag` schemas, assign schema ownership, grant reporting access, and grant `rag_owner` USAGE on the `app` schema. They must not grant table-level access to `.NET`-owned tables because `postgres-init` runs before EF migrations create or rename those tables. `.NET` EF migrations apply the conditional `SELECT` grants for `app.document_permissions` and `app.user_ai_budget_limits`.

## Logger Setup

### Serilog (.NET, `Program.cs`)

```csharp
builder.Host.UseSerilog((ctx, sp, cfg) => cfg
    .Enrich.FromLogContext()
    .Enrich.WithCorrelationIdHeader("X-Request-ID")
    .Enrich.WithProperty("service", "dotnet-api")
    .WriteTo.File(
        formatter: new RenderedCompactJsonFormatter(),
        path: "/var/log/dotnet-api/log-.json",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: ctx.Configuration.GetValue<int>("LogRetentionDays", 30)));
```

### structlog (FastAPI, `core/logging.py`)

```python
import logging, structlog
from asgi_correlation_id import correlation_id

def configure_logging(retention_days: int = 30) -> None:
    logging.basicConfig(level=logging.INFO, format="%(message)s")
    structlog.configure(
        processors=[
            structlog.contextvars.merge_contextvars,
            structlog.processors.add_log_level,
            structlog.processors.TimeStamper(fmt="iso"),
            _add_request_id,
            structlog.processors.JSONRenderer(),
        ],
        wrapper_class=structlog.make_filtering_bound_logger(logging.INFO),
        logger_factory=structlog.PrintLoggerFactory(),
    )

def _add_request_id(_, __, event_dict):
    rid = correlation_id.get()
    if rid:
        event_dict["request_id"] = rid
    return event_dict
```

Daily file rotation in FastAPI uses a `TimedRotatingFileHandler` configured on the root logger with `when="midnight"` and `backupCount=LOG_RETENTION_DAYS`.

## System Prompt (RAG)

```markdown
<!-- services/rag-api/src/advanced_rag/rag/prompts/system_v1.md -->
You are an internal assistant for a corporate document management platform.

Rules:
- Answer in Spanish (Argentine Spanish, "es-AR"). Use a clear, neutral, professional tone.
- Base your answer **only** on the retrieved context provided as a list of chunks below.
- If the retrieved context does not contain enough information to answer, say so explicitly in Spanish and do not invent facts.
- Cite the chunks you used by referencing their `chunk_id`, `document_id`, and `document_version_id` in the structured `citations` array of your response.
- Do not include URLs, internal identifiers, or chunk content verbatim in the `answer` text unless the user asked for an exact quote.
- Never disclose system documents or chunk metadata.

The retrieved context follows:
{context_chunks}
```

The active prompt version is loaded at process startup and recorded per audit row as `prompt_version`.
