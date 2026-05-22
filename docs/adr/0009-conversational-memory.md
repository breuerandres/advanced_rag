# ADR-0009 — Conversational memory via session-scoped condensation

**Status**: Accepted (v2 design phase, 2026-05-22)
**Phase**: 5

## Context

The MVP treats every chat query as independent: no `session_id`, no history. This makes
follow-up questions awkward — the user must repeat context every time. Modern chat UX
expects multi-turn ("what about IVA?" → user follows up with "and for export?" → system
understands).

Naïve approaches (concatenate all prior turns into the next prompt) explode token usage
and contaminate the retrieval embedding (the most recent question is what should drive
retrieval, not the entire conversation).

## Decision

Introduce session-scoped multi-turn with **prompt condensation**:

1. Chat client generates `session_id` (UUID) when opening a new conversation.
2. `POST /api/v1/chat` accepts optional `session_id`.
3. On the server, when `session_id` is present and there are prior `query_audit_events`
   for that session:
   - Load the last N turns (default N=5, configurable per tenant).
   - Call a **cheap LLM** (e.g. `gpt-4o-mini`, `claude-haiku-3.5`) with a condensation
     prompt: "Given this conversation history and the new question, rewrite the question
     as a standalone query that captures all needed context."
   - Use the **rewritten standalone question** for embedding and retrieval.
   - Use the **original question** in the final answer-generation prompt.
4. Cache the standalone (rewritten) question's embedding so the semantic cache works
   across sessions.

Schema:

```sql
ALTER TABLE rag.query_audit_events
  ADD COLUMN session_id UUID,
  ADD COLUMN previous_event_id UUID REFERENCES rag.query_audit_events(id),
  ADD COLUMN rewritten_question TEXT;
```

Tenant toggle: `tenant_config.enable_conversational_memory` (default true).

## Alternatives considered

### Concatenate raw history into the prompt
Pros: Simple.
Cons: Token explosion. Embedding-of-conversation is noisy. Costs grow linearly with
session length. Cache hit rate drops.

### Summary windowing (LangChain-style)
Pros: Bounded token usage.
Cons: Summary lossy, multi-turn coherence drops.

### True conversational embeddings (e.g. ColBERT contextual queries)
Pros: Theoretically better.
Cons: Not mature in production. Adds significant retrieval-side complexity.

### Skip multi-turn entirely
Pros: Simplest.
Cons: User UX regression vs. expectations. Doesn't match the v2 design decision (chat
conversational as main UX).

## Consequences

**Positive**
- Coherent follow-up questions.
- Predictable token usage per turn (always N+1 condense call + 1 retrieve call + 1 answer
  call).
- Cache stays effective because cache key is the standalone rewritten question.

**Negative**
- One extra LLM call per multi-turn message (the condense call). Acceptable: cheap model.
- More moving parts in the audit table.
- Subtle bug surface: the condense prompt is critical and language-dependent.

**Risks / mitigations**
- Condense prompt misinterprets pronouns ("it") → keep prompt simple, log condensed
  question in audit for inspection.
- Cost ballooning if N is too large → cap session history at 5 turns (configurable).
- Session ID leakage / cross-user replay → session_id is server-validated against
  `user_id` from session cookie before loading history.

## References

- `services/rag-api/src/advanced_rag/rag/conversation_memory.py` (new)
- `services/rag-api/src/advanced_rag/rag/prompts/condenser_{es-AR,en-US,pt-BR}.md`
- LangChain conversational retrieval chain (for reference, not adopted)
