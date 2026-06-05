# Chat Conversation Slice A Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `chat-web` a real conversational surface with persisted per-user sessions, reloadable history, and backend question condensation for follow-up questions.

**Architecture:** FastAPI remains the owner of chat/RAG and uses `rag.query_audit_events` as the persistence source for chat turns. The frontend creates a stable `sessionId` per conversation, sends it on every chat request, lists sessions from FastAPI, and renders a transcript instead of a single latest answer. Conversation memory is bounded: the backend rewrites follow-up questions from the latest audited turns, stores `rewritten_question`, and uses the rewritten question for retrieval/cache while preserving the original user question in audit.

**Tech Stack:** React 18, TypeScript, Tailwind/shared UI, FastAPI, Pydantic v2, SQLAlchemy async, PostgreSQL, pytest, Vitest, `uv`, `pnpm`.

---

## Human-Owned Checkpoints

- User-owned local service startup is not required for deterministic Slice A implementation.
- User-owned browser/Compose acceptance happens after agent verification: open `chat.localhost`, ask two follow-up questions in one conversation, reload the page, confirm the conversation remains visible in the left rail and the transcript reloads.
- Agent-owned work is code, migrations are not expected because `session_id` and `rewritten_question` already exist, tests, context updates, and local static verification.

## File And Responsibility Map

### FastAPI RAG

- Modify: `services/rag-api/src/advanced_rag/schemas/chat.py`
  - Add response schemas for session summaries and session turn history.
- Modify: `services/rag-api/src/advanced_rag/api/routers/chat.py`
  - Add `GET /api/chat/sessions` and `GET /api/chat/sessions/{session_id}`.
  - Keep session validation and rate/CSRF boundaries consistent with existing chat routes.
- Modify: `services/rag-api/src/advanced_rag/rag/chat_service.py`
  - Use `session_id` to load bounded history, rewrite follow-up questions, and persist `rewritten_question`.
  - Add read methods for session list and transcript.
- Modify: `services/rag-api/src/advanced_rag/rag/conversation_memory.py`
  - Fix history ordering and keep the condenser best-effort.
- Tests:
  - Modify: `services/rag-api/tests/test_chat_rag.py`

### Chat Frontend

- Modify: `apps/chat-web/src/api/chat.ts`
  - Send `sessionId` and `locale` with chat requests.
  - Add typed clients for session list and transcript.
  - Parse SSE incrementally enough to preserve current behavior; full browser streaming remains a later UI refinement if needed.
- Modify: `apps/chat-web/src/App.tsx`
  - Replace single-answer state with `ConversationTurn[]`.
  - Load sessions on boot, create a UUID for new conversations, and reload transcript when a session is selected.
  - Attach citations and feedback to the assistant turn that produced them.
- Modify: `apps/chat-web/src/App.css`
  - Refit the center panel for transcript messages and stable scroll.
- Tests:
  - Modify: `apps/chat-web/src/App.test.tsx`

### Context

- Modify: `context/rag-spec.md`
  - Move bounded same-user conversation memory into scope.
- Modify: `context/ui-context.md`
  - Document the conversational chat UI shape.
- Modify: `context/progress-tracker.md`
  - Record Slice A in progress/completed status and verification.
- Modify: `context/design-decisions.md`
  - Add the durable decision for persisted chat sessions and bounded memory.

## Task 1: Backend Conversation Contract

**Files:**
- Modify: `services/rag-api/tests/test_chat_rag.py`
- Modify: `services/rag-api/src/advanced_rag/schemas/chat.py`
- Modify: `services/rag-api/src/advanced_rag/api/routers/chat.py`
- Modify: `services/rag-api/src/advanced_rag/rag/chat_service.py`

- [x] **Step 1: Write failing backend tests**

Add tests proving:

```python
def test_chat_persists_session_id_and_rewritten_question_for_follow_up() -> None:
    # Seed two accessible chunks and ask in the same session.
    # The second question is rewritten by a fake condenser-capable provider.
    # Assert audit.question is the original follow-up and audit.rewritten_question is the standalone query.
```

```python
def test_chat_session_list_and_history_are_user_scoped() -> None:
    # Seed audit rows for two users.
    # Assert GET /api/chat/sessions returns only the current user's session.
    # Assert GET /api/chat/sessions/{id} returns ordered turns with citations.
```

- [x] **Step 2: Run backend tests and verify RED**

Run:

```powershell
Set-Location services\rag-api
uv run pytest tests/test_chat_rag.py -q
Set-Location ..\..
```

Expected: failures because the read endpoints and condensation integration do not exist.

- [x] **Step 3: Implement backend contract**

Implement:

- `ChatSessionSummary` with `sessionId`, `title`, `lastQuestion`, `lastAnswer`, `lastActivityAt`, and `turnCount`.
- `ChatSessionTurn` with `queryAuditEventId`, `question`, `answer`, `createdAt`, `cacheHit`, `feedbackValue`, `feedbackComment`, and `citations`.
- `GET /api/chat/sessions`.
- `GET /api/chat/sessions/{session_id}`.
- `ChatService.answer()` condensation when `session_id` is provided and prior turns exist.
- `rewritten_question` persistence in `_insert_audit()`.

- [x] **Step 4: Run backend tests and verify GREEN**

Run:

```powershell
Set-Location services\rag-api
uv run pytest tests/test_chat_rag.py -q
Set-Location ..\..
```

Expected: chat RAG tests pass.

## Task 2: Frontend Conversational UI

**Files:**
- Modify: `apps/chat-web/src/api/chat.ts`
- Modify: `apps/chat-web/src/App.tsx`
- Modify: `apps/chat-web/src/App.css`
- Modify: `apps/chat-web/src/App.test.tsx`

- [x] **Step 1: Write failing frontend tests**

Add tests proving:

```ts
test('loads persisted conversations and renders the selected transcript after reload', async () => {
  // Mock session, chat session list, and transcript responses.
  // Render App and assert the left rail plus both user and assistant messages are visible.
})
```

```ts
test('sends a stable sessionId for follow-up questions in the active conversation', async () => {
  // Ask twice in one conversation.
  // Assert both POST /api/chat bodies include the same sessionId.
})
```

- [x] **Step 2: Run frontend tests and verify RED**

Run:

```powershell
pnpm.cmd --dir apps\chat-web test -- --run src/App.test.tsx
```

Expected: failures because the frontend does not load persisted sessions or send `sessionId`.

- [x] **Step 3: Implement frontend state and API client**

Implement:

- `listChatSessions()`.
- `getChatSession(sessionId)`.
- `submitQuestion({ question, sessionId, locale })`.
- `ConversationTurn[]` rendering in the center panel.
- Session list reload after each completed answer.
- Active transcript reload when selecting a session.

- [x] **Step 4: Run frontend tests and verify GREEN**

Run:

```powershell
pnpm.cmd --dir apps\chat-web test -- --run src/App.test.tsx
```

Expected: chat-web App tests pass.

## Task 3: Context And Verification

**Files:**
- Modify: `context/rag-spec.md`
- Modify: `context/ui-context.md`
- Modify: `context/progress-tracker.md`
- Modify: `context/design-decisions.md`

- [x] **Step 1: Update context files**

Record:

- Chat now supports same-user persisted sessions.
- Bounded condensation uses prior audited turns in the same `session_id`.
- Conversation history is user-scoped and stored as RAG audit evidence.
- Cross-session memory remains out of scope.

- [x] **Step 2: Run focused verification**

Run:

```powershell
Set-Location services\rag-api
uv run pytest tests/test_chat_rag.py tests/test_locale_support.py -q
uv run ruff check .
Set-Location ..\..
pnpm.cmd --dir apps\chat-web typecheck
pnpm.cmd --dir apps\chat-web test -- --run src/App.test.tsx
pnpm.cmd --dir apps\chat-web build
git diff --check
```

Expected: focused backend and frontend verification pass. `git diff --check` may report existing line-ending warnings only; any new whitespace errors must be fixed.
