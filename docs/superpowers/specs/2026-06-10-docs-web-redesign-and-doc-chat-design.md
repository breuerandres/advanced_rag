# docs-web Redesign + Document-Scoped Mini Chat — Design Spec

- **Date:** 2026-06-10
- **Status:** Approved direction (user), pending spec review
- **Scope:** `apps/docs-web`, `services/rag-api`, `infra/compose/Caddyfile`, context docs
- **Out of scope:** `apps/chat-web`, `apps/manage-web`, `services/dotnet-api`, `packages/shared-ui` (consume only, no changes)

## 1. Summary

Redesign the document viewer app (`docs-web`) into a warm help-center experience, add clear
navigation from a document back to the library index, and add an **ephemeral, document-scoped
mini chat** (floating bubble) that answers questions strictly from the open document via the
existing FastAPI RAG pipeline.

User decisions captured during brainstorming:

| Question | Decision |
| --- | --- |
| Primary usage | Mixed: portal index and citation-focused viewer both matter |
| Mini chat persistence | **Ephemeral** — lives while the document is open; lost on reload |
| Mini chat placement | **Floating bubble** (FAB bottom-right) opening an overlay panel |
| Mini chat capabilities | **Streaming answer + 👍/👎 feedback**; no citations UI, no section references |
| Visual direction | **Warm help center**: generous reading typography, comfortable column width, soft surfaces, teal accent, clean cards |
| Viewer reading aids | **Only** "back to library" navigation — no TOC, no related docs, no progress bar |
| Doc-chat transport | **Caddy routes doc-chat paths on the docs host directly to FastAPI** (same pattern as `chat.*`) |
| Code sharing | Mini chat logic is **local to docs-web**; reuse existing `@helpcenter/shared-ui` primitives only. chat-web untouched |

## 2. Architecture change (record in `context/design-decisions.md` + `context/architecture.md`)

**Before:** docs-web calls only the .NET API (`docs.* /api/* → dotnet-api`).

**After:** docs-web calls .NET for everything it does today, **plus** FastAPI for exactly two
same-origin paths: `POST /api/chat` (document-scoped question) and `POST /api/feedback/{id}`.
This mirrors the already-accepted `chat.*` host pattern: FastAPI validates the browser
`__Host-session` cookie by calling .NET `/internal/session/validate` on every request, and the
CSRF double-submit token issued by .NET on the docs host is validated by FastAPI's
`validate_csrf_request`. No new auth mechanism is introduced.

The architecture invariant changes from "manage/docs → .NET only" to:
**"manage → .NET only; docs → .NET, plus FastAPI for document-scoped chat/feedback only;
chat → FastAPI for chat/feedback and .NET for auth/session/viewer-link."**

Rejected alternative: proxying chat through .NET. Rejected because .NET would need to become an
SSE pass-through proxy, duplicating error surface and putting chat responsibility in the backend
that by design never touches RAG.

### 2.1 Caddyfile change (`infra/compose/Caddyfile`)

In the `docs.{$PUBLIC_DOMAIN}` site block, add a `@rag` handle **before** the existing `@api`
handle (Caddy `handle` blocks evaluate in file order; first match wins):

```caddyfile
docs.{$PUBLIC_DOMAIN} {
  tls internal
  import security_headers

  @rag path /api/chat /api/feedback /api/feedback/*
  handle @rag {
    reverse_proxy rag-api:8000 {
      flush_interval -1
    }
  }

  @api path /api/*
  handle @api {
    reverse_proxy dotnet-api:8080
  }

  handle {
    reverse_proxy docs-web:80
  }
}
```

Deliberately **not** routed on the docs host: `/api/chat/sessions*` (doc chat is ephemeral; the
session list/history endpoints stay private to `chat.*`). `/api/chat` is an exact path match, so
`/api/chat/sessions` still falls through to `@api` → .NET → 404. Verify with
`docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml config`.

## 3. FastAPI changes (`services/rag-api`)

### 3.1 Request schema — `src/advanced_rag/schemas/chat.py`

Add an optional document scope to `ChatRequest`:

```python
class ChatRequest(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    question: str = Field(min_length=1, max_length=4000)
    session_id: UUID | None = Field(default=None, alias="sessionId")
    document_id: UUID | None = Field(default=None, alias="documentId")  # NEW: doc-scoped chat
    filters: ChatFilters | None = None
    locale: str | None = None
```

### 3.2 Router — `src/advanced_rag/api/routers/chat.py`

`post_chat` passes the new field through (no other router change; rate limiting, CSRF and
session validation are shared):

```python
answer = await service.answer(
    question=body.question,
    claims=claims,
    request_id=request_id,
    filters=filters,
    session_id=body.session_id,
    locale=body.locale,
    scope_document_id=body.document_id,   # NEW
)
```

### 3.3 Service — `src/advanced_rag/rag/chat_service.py`

`ChatService.answer` gains `scope_document_id: UUID | None = None` (keyword-only, like the rest).
When `scope_document_id is not None` the behavior changes exactly as follows:

1. **Corpus is forced to `"published"`** regardless of role/claims. The mini chat only exists on
   published documents (see §4.5) and this preserves the "public chat retrieves only Published
   content" invariant for every role.
2. **Semantic cache is bypassed entirely** — skip `_lookup_cache` *and* `_write_cache`. Rationale:
   the cache key (`corpus, access_scope_hash, filters_hash`) does not partition by document, and
   per-document Q&A has low cross-user reuse; bypassing is simpler and provably correct. Record
   this in `design-decisions.md`.
3. **Retrieval is filtered to the document** via `HybridRetrievalParams.scope_document_id` (§3.4).
   The branch-aware access predicate still applies unchanged, so a user who cannot access the
   document simply retrieves zero chunks (no existence leak).
4. **No-results copy is document-scoped.** Add alongside `_no_results_message`:

   ```python
   def _no_results_message_scoped(locale: str) -> str:
       if locale.startswith("es"):
           return "No encontré información en este documento para responder esa pregunta."
       return "I couldn't find information in this document to answer that question."
   ```

   Used instead of `_no_results_message` when `scope_document_id` is set and `chunks` is empty.
5. **Audit records the scope.** `_insert_audit` gains a `scope_document_id: UUID | None`
   parameter persisted to a new column (§3.5). Both the generated-answer path and the
   empty-chunks path persist it. (The cache-hit path can never run with a scope because of
   item 2, but pass `scope_document_id=None` explicitly there for clarity.)
6. **Conversation memory is unchanged**: docs-web sends a fresh client `sessionId` per opened
   document, so follow-up questions within the mini chat are condensed with the same
   `condense_question` flow chat-web uses. Budget enforcement (`_enforce_budget`) and pricing
   are unchanged and apply to doc-scoped requests identically.

### 3.4 Retrieval — `src/advanced_rag/rag/hybrid_retrieval.py`

```python
class HybridRetrievalParams(BaseModel):
    ...existing fields...
    scope_document_id: UUID | None = None  # NEW
```

Add to **both** CTEs (`vector_candidates` and `bm25_candidates`), next to the existing
corpus/is_active conditions:

```sql
AND (
    CAST(:scope_document_id AS uuid) IS NULL
    OR chunk.document_id = :scope_document_id
)
```

And bind in `hybrid_retrieve`:

```python
"scope_document_id": str(params.scope_document_id) if params.scope_document_id else None,
```

`ChatService._retrieve_chunks` forwards `scope_document_id=scope_document_id` into the params.

### 3.5 Alembic migration — new revision in `services/rag-api/alembic/versions/`

Follow the existing migration pattern in `context/code-patterns.md`. Single additive column:

```python
def upgrade() -> None:
    op.add_column(
        "query_audit_events",
        sa.Column("scope_document_id", sa.Uuid(), nullable=True),
        schema="rag",
    )

def downgrade() -> None:
    op.drop_column("query_audit_events", "scope_document_id", schema="rag")
```

`_insert_audit`'s SQL adds the column + bind parameter. No backfill needed (NULL = corpus-wide).

### 3.6 Session list exclusion — `ChatService.list_sessions`

Doc-scoped turns must **not** appear as conversations in chat-web's drawer. In the `ranked` CTE
of `list_sessions`, extend the WHERE clause:

```sql
where user_id = :user_id
  and session_id is not null
  and scope_document_id is null   -- NEW: hide ephemeral doc-chat turns
```

`get_session_history` stays unchanged (it is only reachable from ids returned by the list).

### 3.7 Error codes

No new codes. Doc-scoped chat reuses: `VALIDATION_FAILED` (400), `AUTH_REQUIRED` (401),
`CHAT_RATE_LIMITED` (429), `AI_BUDGET_EXCEEDED` (429), `RAG_PROVIDER_MISCONFIGURED` (500).

### 3.8 SSE contract (unchanged, documented here for the implementer)

`POST /api/chat` responds `text/event-stream` with events in this order:
`request-id` → [`cache-hit`] → `answer-token` (1..n; **today exactly 1** carrying the full
answer — the frontend must still accumulate deltas generically) → `citations` (carries
`query_audit_event_id`, needed for feedback) → `usage` → `done`. Errors raised before
generation (budget, rate limit, validation, auth) return a normal JSON error envelope with the
matching HTTP status, **not** an SSE stream. The doc-chat client must handle both shapes —
copy the proven parser from `apps/chat-web/src/api/chat.ts` (`processSseBuffer`,
`processSseEvent`, `parseChatReadableStream`).

## 4. docs-web frontend

### 4.1 File structure (split the 444-line `App.tsx`)

```
apps/docs-web/src/
  App.tsx                          # query-param dispatch only (documentId? viewer : portal)
  api/
    viewer.ts                      # existing .NET client (small edits, §4.6)
    docChat.ts                     # NEW: askDocument() SSE client + submitDocFeedback()
  features/
    portal/
      DocumentPortalApp.tsx        # session/handoff bootstrap + portal states
      DocumentPortal.tsx           # search, type chips, card grid
      DocumentCard.tsx             # one card
    viewer/
      ViewerLinkApp.tsx            # handoff consumption + viewer states
      DocumentView.tsx             # top bar, title block, content column
    docChat/
      DocChatWidget.tsx            # FAB + panel + transcript + composer + feedback
      useDocChat.ts                # ephemeral chat state machine
    auth/
      DocsLoginPage.tsx            # login (moved, localized, lang/theme controls)
```

No `react-router` is introduced. Routing stays what it is today: `?documentId=` renders the
viewer, otherwise the portal; "back to library" is a plain `<a href="/">` (the docs host session
cookie is already established by then, so `/` renders the portal directly).

### 4.2 Visual direction — "warm help center"

Constraints from `context/ui-context.md` still apply: no decorative gradients, no hero/marketing
composition, no glass/glow, minimal functional motion (`transform`/`opacity` only), shared dark
token palette, 6px radius family, teal primary, visible focus states.

Within that, the docs surfaces shift from "dense admin" to "comfortable reading":

- **Palette (light):** keep warm cream page `#fff8f5`, white surfaces, teal accent `#00685f`
  (selected/CTA), warm borders `#e2d8d2`. Type chips use soft tints (see below). Dark mode uses
  the existing `var(--bg…)`/`var(--fg…)`/`var(--accent)` tokens exactly as today — every new
  element must have a `[data-theme='dark']` mapping.
- **Typography:** UI stays the system sans. Document content (`.document-content`) becomes
  reading-first: `font-size: 17px; line-height: 1.7;` inside a centered column of
  `max-width: 72ch`. Portal/page H1 32px, card titles 17px/600. No external font downloads
  (single-tenant offline deployments).
- **Document-type chips:** small rounded chips with a lucide icon and a tint derived from a
  fixed 6-color warm palette: index = sum of the type string's UTF-16 char codes modulo 6 (same
  spirit as manage-web's `UnitLevelBadge` `level % 6`). Define the six tint pairs once in
  `App.css` with dark variants.
- **Cards:** whole card is the click target (a `<button>`/`<a>` with accessible name = title).
  Content: type chip, title, audience line (muted), footer row with relative updated date
  ("Actualizado hace 3 días" via `Intl.RelativeTimeFormat` on `updatedAt`) and a state badge
  **only when `state !== 'Published'`** (managers see drafts in the catalog). Hover: 1px border
  accent + `transform: translateY(-1px)` + slightly stronger shadow; transition ≤150ms.
  Remove the current `dl` Estado/Grupos block and the "Abrir documento" button.
- **Motion:** chat panel opens with `opacity` + `transform: translateY(8px)` 150ms ease-out.
  Nothing else animates beyond hover/focus transitions.

### 4.3 Portal (index) layout

```
┌────────────────────────────────────────────────────────────┐
│ ◈ Centro de Ayuda                          [ES ▾] [☾]      │
│                                                            │
│   Biblioteca de documentos                                 │
│   Encontrá guías y documentación según tus permisos.       │
│   [🔍  Buscar por título, tipo o audiencia…      ]         │
│                                                            │
│   (Todos) (Política) (Manual) (Procedimiento) …            │
│                                                            │
│   ┌─────────┐  ┌─────────┐  ┌─────────┐                    │
│   │ ◷ chip  │  │ chip    │  │ chip    │   ← card grid      │
│   │ Título  │  │ Título  │  │ Título  │                    │
│   │ aud.    │  │ aud.    │  │ aud.    │                    │
│   │ hace 3d │  │ hace 1m │  │ hace 2h │                    │
│   └─────────┘  └─────────┘  └─────────┘                    │
└────────────────────────────────────────────────────────────┘
```

- **Filter chips switch from access groups to `documentType`** (unique values present in the
  catalog, sorted, prefixed with "Todos"). Access groups are plumbing, not navigation; remove the
  group strip and any `allowedGroups` usage from search matching (search matches title, type,
  audience). The API response keeps `allowedGroups`; the UI just stops consuming it.
- Search input: bounded width (≤560px), 44px tall, lucide `Search` icon, localized placeholder.
- Empty state: distinguish "no documents available" from "no matches for current filters"
  (clear-filters action on the latter).
- Loading: 6 skeleton cards (`Skeleton` from shared-ui) instead of the current single-line state.
- Open document keeps today's logic: `createViewerLink(id, purpose)` then
  `window.location.assign(url)`. **Role fix (§4.6):** purpose `management` is used when the user
  has any of `Admin`, `DocumentEditor`, `DocumentPublisher` (legacy `DocumentManager` removed).

### 4.4 Viewer layout

```
┌────────────────────────────────────────────────────────────┐
│ ← Volver a la biblioteca          ◈ Centro de Ayuda  ES ☾  │  ← sticky top bar
├────────────────────────────────────────────────────────────┤
│        (Manual) (General)  · Actualizado hace 3 días       │
│        Manual de Onboarding                                │  ← title block
│        ─────────────────────────────────────────           │
│                                                            │
│        Contenido del documento (columna 72ch,              │
│        17px/1.7) …                                         │
│                                                            │
│        Sesión válida hasta 14:30 · Acceso verificado       │  ← quiet footer line
│                                                     (✦)    │  ← chat FAB
└────────────────────────────────────────────────────────────┘
```

- **Sticky top bar** with `← Volver a la biblioteca` (`<a href="/">`, lucide `ArrowLeft`,
  localized) on the left; language select + dark-mode toggle on the right. This is the requested
  document → index navigation.
- **Title block:** document-type chip + audience chip + relative updated info, then the H1.
- **The right side rail is removed.** Its metadata moves to the title-block chips; session
  expiry (`tokenExpiresAt`) becomes one quiet muted line at the end of the document (plus the
  existing error states when the session actually expires). The current `trust-strip`
  ("Acceso verificado / Sesion activa") is replaced by that single line — same reassurance,
  less chrome.
- **Draft/review banner:** when `state !== 'Published'`, show a tinted banner under the top bar
  ("Estás viendo una versión en borrador / en revisión — no visible para usuarios finales").
  Uses the same semantic state colors as manage-web badges.
- Existing rich-content CSS (lists, tables, code, `<mark>`, images, blockquote) is preserved and
  re-themed, not removed. Document HTML rendering stays exactly as is: docs-web renders the
  server-sanitized HTML (`Ganss.Xss` on save) directly; `dompurify` is not a docs-web dependency
  and must not be added in this change set.
- Loading/error states keep `EmptyState` with icons, restyled to the new surface.

### 4.5 Document-scoped mini chat (`DocChatWidget`)

**Visibility rule:** rendered **only** when `document.state === 'Published'`. Draft/review views
never show the FAB (retrieval only indexes published content; don't offer a chat that cannot
answer).

**FAB:** fixed bottom-right (24px offsets), 52px circle, accent background, lucide
`MessageCircleQuestion` icon, `aria-label` + tooltip "Preguntale a este documento". When the
panel is open the FAB becomes a close affordance (icon swaps to `X`).

**Panel:** fixed above the FAB, width 380px, `max-height: min(70vh, 560px)`, `role="dialog"`,
labelled by its header. On viewports `≤720px` it becomes a bottom sheet: full width,
`height: 75dvh`. Closing the panel **keeps** the conversation (component stays mounted, panel
hidden); a full page reload loses it — that is the agreed ephemeral behavior.

Panel structure:

1. **Header:** title "Preguntale a este documento", subtitle "Respuestas basadas solo en este
   documento.", close button.
2. **Transcript:** scrollable list, `aria-live="polite"`. User bubbles right-aligned (subtle
   background), assistant bubbles left-aligned. Assistant content renders through shared-ui
   `Markdown`; user content is plain text. Do **not** reuse shared-ui `ChatMessage` (its
   badge-and-label layout is too heavy for a 380px panel); build two small local bubble styles.
3. **Empty state:** short hint + two static suggested questions (i18n strings, not generated);
   clicking one submits it.
4. **Typing indicator:** pulsing dot bubble between submit and the first `answer-token`.
5. **Feedback:** under each completed assistant turn, 👍/👎 buttons (selectable and deselectable
   before submitting, mirroring chat-web), optional comment input appears once a value is chosen,
   with send action. States: idle → sending → sent ("Gracias por tu feedback", value editable
   again per MVP rule) → retryable error. Uses `submitDocFeedback(queryAuditEventId, value,
   comment)`.
6. **Composer:** reuse shared-ui `ChatComposer` (autosizing textarea, Enter submits, disabled
   while streaming), localized `placeholder`/`submitLabel`/`pendingLabel` props.

**State machine (`useDocChat.ts`):**

```ts
interface DocChatTurn {
  key: string                       // stable client key
  question: string
  answer: string                    // grows during streaming
  status: 'pending' | 'streaming' | 'done' | 'error'
  queryAuditEventId: string | null  // set from the citations event; null until done
  feedback: { value: 'up' | 'down' | null; comment: string;
              status: 'idle' | 'sending' | 'sent' | 'error' }
}
// Hook state: turns: DocChatTurn[], sessionId: string (crypto.randomUUID() once per mount),
// isStreaming: boolean, error: string | null (panel-level, for pre-stream failures)
```

- `ask(question)`: appends a pending turn, calls `askDocument({ question, documentId, sessionId,
  locale }, { onAnswerToken })`; tokens append to `answer` and flip status to `streaming`; on
  resolve, set `queryAuditEventId`, status `done`. On reject **remove the partial turn** and show
  an inline error row with the localized message and a "Reintentar" button that re-submits the
  same question (mirrors chat-web's partial-answer rollback; do not try to pre-fill
  `ChatComposer` — it owns its own input state).
- Error mapping (panel-level, localized): `AI_BUDGET_EXCEEDED` → friendly service-limited copy
  (no pricing internals; not presented as account lockout), `CHAT_RATE_LIMITED` → "esperá unos
  segundos", `AUTH_REQUIRED` → session-expired copy, anything else → generic retry copy.

**API client (`api/docChat.ts`):** copy `submitQuestion`/SSE parsing/`submitFeedback` from
`apps/chat-web/src/api/chat.ts`, trimmed to what the widget needs (`askDocument` adds
`documentId` to the POST body; drop sessions/viewer-link/logout functions). CSRF comes from the
docs host `/api/csrf` exactly as `viewer.ts` already does — reuse one shared CSRF helper between
`viewer.ts` and `docChat.ts` (extract to `lib/csrf.ts`) instead of duplicating the token holder.

### 4.6 `api/viewer.ts` edits

- `viewerErrorMessage(error)` (hardcoded Spanish) becomes `viewerErrorKey(error): string`
  returning an i18n key; components render `t(viewerErrorKey(e))`. Map:
  `AUTH_REQUIRED → 'errors.auth_required'`, `AUTH_FORBIDDEN → 'errors.forbidden'`,
  `NOT_FOUND → 'errors.not_found'`, `VIEWER_HANDOFF_* → 'errors.handoff_*'`, fallback
  `'errors.generic'`. This fixes the existing violation of the "map stable error codes to
  localized messages" rule.
- Extract the CSRF/`requestJson` helpers shared with `docChat.ts` (no behavior change).
- No endpoint changes.

### 4.7 Role gating fix (in-flight access refactor alignment)

`DocumentPortal` currently checks `user.roles.includes('DocumentManager')` (legacy). Replace:

```ts
const MANAGEMENT_ROLES = ['Admin', 'DocumentEditor', 'DocumentPublisher'] as const
const isManagementUser = user.roles.some((role) => (MANAGEMENT_ROLES as readonly string[]).includes(role))
```

`displayState` gains the missing `Archived` label and moves to i18n keys (`states.published`,
`states.draft`, `states.in_review`, `states.archived`).

### 4.8 i18n — full key changes (`src/i18n/es-AR.json`, `src/i18n/en-US.json`)

**Delete** (never referenced in code — aspirational scaffold keys): `viewer.table_of_contents`,
`viewer.breadcrumb_home`, `viewer.previous`, `viewer.next`, `viewer.favorite`,
`viewer.unfavorite`, `viewer.share`, `viewer.last_updated`, `viewer.feedback_prompt`,
`viewer.feedback_helpful`, `viewer.feedback_not_helpful`, `viewer.feedback_thanks`,
`viewer.metrics_views`, `viewer.metrics_likes`, `viewer.metrics_favorites`, the whole `browse.*`
group, and `viewer.not_found` / `viewer.access_denied` / `viewer.session_expired` (replaced by
the consolidated `errors.*` group below).

**Add / replace** — es-AR copy (en-US mirrors in English; all UI literals currently hardcoded in
JSX move into these keys; voseo, no leading-space punctuation, proper accents — note several
existing strings like "Iniciá sesión" are currently mojibake-free but others ("Inicia sesion")
are not; normalize while moving):

```jsonc
{
  "portal": {
    "title": "Biblioteca de documentos",
    "subtitle": "Encontrá guías y documentación según tus permisos.",
    "search_placeholder": "Buscar por título, tipo o audiencia…",
    "filter_all": "Todos",
    "filters_label": "Filtrar por tipo de documento",
    "empty_title": "Sin documentos para mostrar",
    "empty_no_access": "Todavía no hay documentos disponibles para tu usuario.",
    "empty_filtered": "No encontramos documentos con los filtros actuales.",
    "clear_filters": "Limpiar filtros",
    "open_document": "Abrir {{title}}",
    "updated_ago": "Actualizado {{when}}"
  },
  "viewer": {
    "back_to_library": "Volver a la biblioteca",
    "session_valid_until": "Sesión válida hasta {{time}} · Acceso verificado",
    "draft_banner": "Estás viendo una versión en borrador. No es visible para usuarios finales.",
    "review_banner": "Estás viendo una versión en revisión. No es visible para usuarios finales.",
    "loading": "Validando enlace…"
  },
  "docChat": {
    "open": "Preguntale a este documento",
    "close": "Cerrar chat",
    "title": "Preguntale a este documento",
    "subtitle": "Respuestas basadas solo en este documento.",
    "empty_hint": "Hacé una pregunta sobre el contenido de este documento.",
    "suggestion_1": "¿De qué trata este documento?",
    "suggestion_2": "Resumime los puntos principales.",
    "placeholder": "Escribí tu pregunta…",
    "send": "Enviar",
    "sending": "Generando respuesta…",
    "retry": "Reintentar",
    "feedback_up": "Respuesta útil",
    "feedback_down": "Respuesta no útil",
    "feedback_comment_placeholder": "Contanos qué mejorarías (opcional)",
    "feedback_send": "Enviar feedback",
    "feedback_thanks": "Gracias por tu feedback.",
    "feedback_error": "No pudimos guardar tu feedback. Probá de nuevo.",
    "error_budget": "Alcanzaste tu límite mensual de uso de IA. Podés seguir leyendo el documento sin problema.",
    "error_rate_limited": "Demasiadas preguntas seguidas. Esperá unos segundos y probá de nuevo.",
    "error_generic": "No pudimos responder tu pregunta. Probá de nuevo."
  },
  "errors": {
    "auth_required": "Iniciá sesión para continuar.",
    "forbidden": "No tenés permiso para abrir este documento.",
    "not_found": "No encontramos el documento solicitado.",
    "handoff_expired": "El enlace de acceso expiró. Volvé a abrir el documento desde la app.",
    "handoff_invalid": "El enlace de acceso no es válido. Volvé a abrir el documento desde la app.",
    "handoff_used": "Este enlace de acceso ya fue usado. Volvé a abrir el documento desde la app.",
    "generic": "No pudimos abrir el documento."
  },
  "states": {
    "published": "Publicado",
    "draft": "Borrador",
    "in_review": "En revisión",
    "archived": "Archivado"
  },
  "login": {
    "eyebrow": "Documentos",
    "title": "Iniciar sesión",
    "email": "Email",
    "password": "Contraseña",
    "submit": "Entrar",
    "failed": "No se pudo iniciar sesión."
  }
}
```

The login page additionally gains the same visible `LanguageSelect` + `DarkModeToggle` controls
manage-web's auth frame exposes (currently missing on docs login).

## 5. Security / invariants checklist

| Invariant | How this design preserves it |
| --- | --- |
| Public chat retrieves only Published content | Doc-scoped path forces `corpus = "published"`; widget only renders on Published documents |
| docs revalidates access server-side | Unchanged for viewing; doc-chat retrieval applies the same branch-aware access predicate in SQL — inaccessible docs yield zero chunks, generic no-info answer |
| Session JWT never in URL | Unchanged; doc-chat uses the `__Host-session` cookie + CSRF header |
| Cache reuse only for matching `access_scope_hash` | Stronger: doc-scoped requests never read or write the semantic cache |
| Audit persists to Postgres | Every doc-chat turn writes `rag.query_audit_events` with `scope_document_id`; feedback rows unchanged |
| AI budgets | `_enforce_budget` runs unchanged; over-budget shows the friendly limited state, never a lockout |
| Sanitized HTML rendering | Unchanged (`DOMPurify` on document HTML); chat answers render via shared `Markdown`, never `dangerouslySetInnerHTML` |
| FastAPI never writes `app` schema | Unchanged — only reads, new column lives in `rag` |

## 6. Testing plan

**rag-api (`uv run pytest tests/test_chat_rag.py -q`, extend the existing fixture):**
1. Doc-scoped question retrieves only chunks of the scoped document (seed two accessible docs;
   assert answer cites/uses only the scoped one via `hybrid_retrieve` direct test with
   `scope_document_id`).
2. Doc-scoped request with a matching semantic-cache entry **does not** return a cache hit, and
   does not insert a cache entry after answering.
3. Doc-scoped audit row persists `scope_document_id`; corpus recorded as `published` even for a
   non-Viewer role.
4. Scoped turns are excluded from `list_sessions`; unscoped turns still listed.
5. Scoped retrieval against a document outside the user's access rules returns zero chunks
   (no-results message path).
6. Migration test pattern: new Alembic revision applies cleanly (existing `test_migrations.py`
   conventions). Also `uv run ruff check .` and `uv run mypy src tests`.

**docs-web (`pnpm.cmd --dir apps\docs-web test -- --run src/App.test.tsx`, typecheck, build):**
1. Portal renders document cards with type chip and relative date; type-chip filter narrows the
   grid; "no matches" empty state shows clear-filters.
2. Viewer renders the back-to-library link pointing to `/`.
3. FAB renders for a Published document; absent for Draft/In Review.
4. Mini chat: submit question → mocked SSE response (single `answer-token` + `citations` +
   `done`, reuse chat-web's test SSE fixtures) renders the answer; feedback 👍 → POST
   `/api/feedback/{id}` called; budget-exceeded JSON error → localized limited-service message;
   stream failure → partial turn removed + retry state.
5. Legacy role test updated: `DocumentEditor` gets `purpose: 'management'` viewer links;
   `DocumentManager` no longer referenced anywhere in `apps/docs-web/src`.
6. i18n: ES and EN render for a representative new key (existing language-switch test pattern).

**Infra:** `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml config`.

## 7. Context/docs updates (same change set)

- `context/design-decisions.md`: entry for (a) docs host → FastAPI routing for doc-chat,
  (b) semantic-cache bypass for doc-scoped requests, (c) ephemeral doc-chat sessions excluded
  from the chat session list.
- `context/architecture.md`: update the frontend-routing invariant (§2 wording) and the audit
  schema note (`scope_document_id`).
- `context/ui-context.md`: rewrite the "Document Viewer UI" section to describe the portal
  (type-chip filters, card grid), viewer (top bar, reading column, no side rail), and the mini
  chat (visibility rule, ephemeral, streaming, feedback).
- `context/code-patterns.md`: no new error codes, no change.
- `context/progress-tracker.md`: status entry on completion.

## 8. Out of scope (explicit)

- Table of contents, related documents, reading progress (user declined).
- Persisted doc-chat history, citations UI inside the mini chat, section references.
- Any change to chat-web, manage-web, shared-ui components, or the .NET API.
- Multimodal answers in the mini chat (query-time multimodal RAG is a separate planned slice).
- New fonts or UI libraries.

## 9. Manual acceptance (user-owned, after implementation)

Through the Compose/Caddy stack (`.\infra\compose\Start-Local.ps1`, seeded data):
1. `https://docs.localhost` → login → portal shows redesigned cards; type chips filter; search works; dark mode and EN/ES hold across portal and viewer.
2. Open a published document → new top bar; `← Volver a la biblioteca` returns to the portal without re-login.
3. FAB appears; ask "¿De qué trata este documento?" → streamed answer about that document only; ask a question whose answer exists only in *another* accessible document → "no encontré información en este documento…".
4. 👍 with comment → row visible in manage-web feedback review; chat-web's conversation drawer does **not** list the doc-chat session.
5. As an over-budget user → friendly limited message; document reading still works.
6. Postman: `POST https://docs.localhost/api/chat` body `{"question":"...","documentId":"<uuid>","sessionId":"<uuid>"}` with session cookie + `X-CSRF-Token` → 200 SSE; same without cookie → 401 envelope; `GET https://docs.localhost/api/chat/sessions` → 404 (not routed).
