# UI Context

## Status

The base UI stack is decided for the MVP. Detailed screen-level layouts still need implementation planning before coding.

## UI Stack

- React 18 with strict TypeScript for all three frontends.
- Vite as the build tool; `pnpm` workspace under `apps/`.
- Tailwind CSS for styling and design tokens.
- `shadcn/ui` as the base component system.
- `lucide-react` for icons.
- `@tanstack/react-query` v5 for server state, `zustand` for the small amount of local state that does not fit URL or query cache.
- `react-hook-form` + `zod` for forms; same `zod` schemas validate API responses.
- `@tiptap/react` for the rich text document editor in `manage-web`. Output is sanitized HTML.
- `dompurify` to sanitize HTML before rendering in the viewer.
- `react-router-dom` v6 for routing.
- See `context/code-standards.md` for library versions and complete list.
- End-user UI supports **Spanish (es-AR)** and **English (en-US)**. Spanish remains the default. See the Language Policy section of `context/code-standards.md` for the full rules.
- Keep shared UI conventions consistent across the three frontends, but each frontend can have workflow-specific layouts.

## Product UI Direction

- The product has three independent React frontends: management, chat, and document viewer.
- Management UI should feel like a dense operational SaaS tool: efficient, scannable, and audit-friendly.
- Chat UI should prioritize fast question asking, citations, answer feedback, and safe links to the document viewer.
- Document viewer UI should prioritize readable document content, access validation states, and clear token expiration handling.
- Avoid landing-page composition, marketing hero sections, decorative cards, and visually noisy gradients in product surfaces.
- Prefer compact tables, forms, filters, detail panels, tabs, dialogs, menus, badges, and status indicators using `shadcn/ui` patterns.
- Use `lucide-react` icons for actions when a familiar icon exists.
- Icon-only action buttons in management surfaces must expose the same text as accessible name and hover/focus tooltip.
- Do not force one global product header onto all three SPAs. Shared UI should provide tokens and primitives; manage, chat, and docs own their workflow-local navigation.
- Dark mode must use the shared token palette across page backgrounds, local headers, sidebars, cards, panels, forms, tables, dialogs, badges, and document/chat content areas.
- Each SPA must expose a visible language selector with ES and EN only. Spanish remains the default unless the user explicitly chooses and persists English.

## Task 17.5 UI Polish Quality Gate

- Use the local Codex skill `advanced-rag-product-ui-polish` as the Task 17.5 UI implementation checklist.
- The skill adapts Cult UI `components-build` and `fixing-motion-performance` guidance to this product, but Cult UI is guidance only; do not add Cult UI as a dependency or copy components without an explicit implementation decision.
- Optimize Task 17.5 in this order: first-run setup API, management setup/login/session shell, reusable local UI primitives where repetition is clear, management polish, chat polish, docs viewer polish, and first-run E2E verification.
- Build complete states rather than static screens: loading, empty, success, validation error, server error, auth expired, API unavailable, budget limited, exchange-code failure, and token-expired states must be visually deliberate.
- Apply a warm enterprise SaaS direction with teal primary actions, compact spacing, 6px radius, clear status badges, high-legibility typography, semantic controls, keyboard access, and visible focus states.
- Keep motion minimal and functional. Prefer `transform` and `opacity`; avoid layout, blur, filter, gradient, or scroll-driven animation for Task 17.5.
- Use anti-generic-UI guardrails from Uncodixfy where they align with this product: no decorative gradients, glass panels, glows, ornamental labels, fake metrics, filler charts, hero sections inside dashboards, oversized radii, pill overload, or transform-heavy hover effects.
- Borrow only the useful workflow idea from `codex-design-skill`: state a concrete design direction before coding and run a final validation pass. Do not install it or follow its Next.js/21st.dev defaults for this Vite React project.

## Management Layout Patterns

- Use persistent navigation for the main management sections: documents, users/groups, audit, feedback, and configuration. Do not add a separate navigation entry for AI budgets because those controls duplicate the users/groups workspace.
- Management navigation is role-aware. Render only sidebar entries the current user can use, and keep API authorization as the enforcement boundary for direct URL or endpoint access.
- AI budget configuration is part of the users/groups workspace because the budget is configured per user and needs role/group context.
- Viewer management access is limited to `Mi cuenta`, own AI balance, and safe read-only configuration; the sidebar must not render documents, full users/groups, audit, or feedback for Viewer users.
- DocumentManager can see documents, users/groups, audit, feedback, configuration, and account self-service, but management actions inside those screens must hide Admin-only controls such as publish, create user, role/status changes, and AI budget edits.
- User deactivation is a logical status change exposed from the users/groups table; inactive users remain visible for audit and recovery.
- User role/group editing and group rename actions live in the users/groups workspace. Users and groups must be separated into workspace tabs so operators can focus on one table at a time. Groups must be visible as their own operational table, not only as values inside user rows.
- Feedback review is a separate management workspace. Chat feedback remains audit evidence tied to RAG query audit rows, but it must not replace functional management audit activity.
- Active session identity and logout controls live at the bottom of the management sidebar, not in a top workspace bar.
- Management account self-service lives in a dedicated `Mi cuenta` section and supports changing the current user's email and password.
- Management dialogs should not duplicate close affordances in the header when the footer already provides cancel and the primary save action closes the dialog.
- The audit workspace is reserved for functional management events and reads `.NET` `/api/audit/events`, backed by `app.audit_events`. Document lifecycle events are visible there, and local demo seeding with `-WithSampleDocument` inserts a sample `document.created` event.
- Use table/list views for operational review workflows, with compact filters above or beside the result set. Search controls should be bounded instead of consuming the full workspace width when paired with short filters.
- Use detail panels or pages for document lifecycle state, version history, audit events, indexing status, and feedback context.
- Document lifecycle status badges use distinct semantic colors for `Draft`, `In Review`, `Published`, and `Archived` so reviewers can scan publication readiness quickly.
- The document list must support filtering across the visible document attributes: search text, lifecycle state, indexing state, document type, audience, access-group coverage, and updated metadata when available.
- Forms must show field-level validation, server errors, dirty state, disabled submission state, and recovery actions.
- The document editor supports assisted PDF/DOCX import through the .NET API: upload, extraction loading state, extraction error state, safe draft HTML inserted into the editor when available, fallback extracted text, and user-controlled formatting before save/review.
- The import upload control must show the 10 MB per-file limit and validate file size before upload when the browser exposes the size.
- If extraction fails because no text is extractable, the UI shows a clear error, keeps current editor content unchanged, and lets the user upload another file or enter content manually.
- If the user cancels or leaves without saving, the UI must treat imported HTML/text as unsaved editor state and discard it like any other unsaved draft changes.

## Feedback Review UI

- `Admin` and `DocumentManager` can access feedback review from the dedicated management feedback workspace.
- The feedback review view uses a dense table/list layout.
- Filters include feedback polarity, negative-only mode, cited document, user, and date range.
- Rows show every chat question and answer summary, including queries that have not received feedback yet. Rows include feedback value or a clear no-feedback state, optional comment, user, timestamp, and cache hit.
- The normal feedback table intentionally omits request ID and citations to keep review scanning compact. Export to Excel/CSV includes all available fields, including request ID, query audit id, user id, cited document/version ids, citation headings, cache hit, timestamps, question, answer summary, feedback value, and comment.
- The empty state must distinguish no feedback exists yet from filters returning no matches.
- The view reads data through the .NET API; it must not call FastAPI directly.

## AI Usage Budget UI

- `Admin` can configure per-user monthly AI usage budgets from the management app.
- AI budget controls live in the users/groups workspace rather than a separate duplicated budget screen.
- All authenticated users can view their own AI usage balance. `DocumentManager` can view user balances for operational context but cannot edit them. Only `Admin` can change or disable budget limits.
- The budget view should show user, role/groups context, current period spend, monthly budget, remaining budget, budget status, last usage timestamp, and actions to adjust or disable the limit.
- Budget values are monetary amounts, initially USD, and are distinct from request-frequency rate limits. The default monthly budget shown for new users is USD 5 unless configured otherwise.
- The budget UI must show the current customer calendar-month period according to the deployment timezone.
- Over-budget chat users must see a clear service-limited state that does not expose internal pricing logic or technical details.
- Budget exhaustion must not be presented as a general account lockout; users can still open authorized documents and use non-AI workflows.

## Chat UI

- Chat runs as an independent product surface without the shared global management header.
- Chat uses a simplified ChatGPT-like layout with a fixed management-style left drawer. The drawer owns conversation history, the new-conversation action, language selection, dark-mode toggle, active-session user context, and logout. The center workspace owns only the active transcript, answer-level citation summary buttons, usage, feedback, error states, and the bottom composer.
- Chat bootstraps by validating the `.NET` browser session and relies on the unified session cookie for chat/feedback requests. It must not renew or store a scoped chat-token cookie.
- The left drawer lists persisted same-user conversations loaded from FastAPI, not local-only browser state. Reloading the page must preserve visible conversation history once the server has stored at least one turn.
- The center panel renders the selected conversation as a transcript with user and assistant turns. A new conversation creates a stable client `sessionId`; it appears in the persisted list after the first successful answer is audited.
- Selecting a conversation reloads its ordered transcript from FastAPI. Each assistant turn owns its citations, feedback value/comment, cache state, and usage state so feedback and citations stay attached to the answer that produced them.
- Each answer exposes thumbs up/down feedback controls and an optional comment entry after the user chooses a feedback value.
- Feedback value buttons are selectable and deselectable before submission; clicking the selected value again clears the local selection.
- Feedback submission must show loading, success, retryable error, and already-submitted states. The same user can update their feedback on an answer in the MVP.
- Citations do not render as inline cards below answers. Each completed answer exposes a deduplicated `Ver citas (n)` control that opens a collapsible right drawer. The right drawer stays fixed to the viewport height while the conversation transcript scrolls independently.
- Citation links open `docs.client.com` with document-id locator links and secure viewer session handoff when needed.
- Citation links use document-id locator URLs. The docs app must revalidate the authenticated session and show safe login, access-denied, or not-found states without exposing credential-bearing URL values.

## Document Viewer UI

- `docs.localhost` root renders an independent authenticated document portal with search, category/group filters, and visible documents determined by the current user. Admins and document managers can see management-scope documents; viewers see only published documents allowed by their groups.
- Document-id URLs render the focused viewer flow for citations and explicit document links.
- The viewer must handle missing session, unauthorized, document-not-found, and successful document states.
- Credential-bearing tokens must never be visible to JavaScript or shown in the URL.

## Management Document Editor

- The management document editor is a full workspace tab inside `Documentos`, not a modal, because draft creation/review is a complex workflow with metadata, access groups, import, validation, and rich editing.
- The management editor uses TipTap with StarterKit plus approved extensions as needed. The toolbar must support paragraph, h1-h3, bold, italic, underline, strike, ordered and unordered lists, blockquote, inline code, code block, horizontal rule, link, table insertion, undo/redo, document image insertion, text color selection, semantic highlight, and paragraph/heading text alignment.
- Output is HTML stored in `app.document_versions.content_html` after server-side sanitization with `Ganss.Xss`.
- Document image insertion uses an upload control backed by the `.NET` document image API, not arbitrary URL prompts. The editor inserts stable same-origin `/api/document-images/{imageId}/content` URLs after upload succeeds.
- New unsaved documents must be saved as a draft before image uploads are available, because image metadata is document-owned.
- The editor must reject or surface server errors for base64 `data:` images and external image URLs instead of silently saving them.
- The editor must support headings (h1-h3), bold/italic/underline, ordered/unordered lists, links, inline code, code blocks, tables, highlight, and text alignment.
- Disallow raw `<script>`, `<iframe>`, `<style>`, non-approved inline `style` attributes, and `on*` handlers. The sanitizer preserves only the `color` and `text-align` CSS properties needed by approved TipTap controls, preserves semantic `<mark>` highlight tags, and strips all other inline CSS properties regardless of UI controls.
- The assisted PDF/DOCX import inserts sanitized draft HTML when available. DOCX imports may preserve common semantic structure; PDF imports use conservative formatting and may still require substantial manual cleanup.

## Chat Streaming

- The chat frontend consumes Server-Sent Events from FastAPI to render the answer progressively.
- Streaming UI must display: typing indicator before the first token, progressive answer body, citation summary control populated once the answer terminates, feedback controls enabled only after stream completion.
- If the SSE connection drops mid-stream, the UI shows a retryable error state. Partial answers are not committed to persisted chat history or the session list.
- Chat Conversation Slice B implements progressive SSE parsing in `chat-web`: `answer-token` events update only the pending transcript turn, while citations, usage, feedback controls, and session-list refresh remain gated on stream completion.

## Open UI Decisions

- Responsive breakpoints.
- Detailed screen layouts for management, chat, and document viewer.
