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
- `@tiptap/react` for the rich text instruction editor in `manage-web`. Output is sanitized HTML.
- `dompurify` to sanitize HTML before rendering in the viewer.
- `react-router-dom` v6 for routing.
- See `context/code-standards.md` for library versions and complete list.
- End-user UI is in **Spanish (es-AR)**. See the Language Policy section of `context/code-standards.md` for the full rules.
- Keep shared UI conventions consistent across the three frontends, but each frontend can have workflow-specific layouts.

## Product UI Direction

- The product has three independent React frontends: management, chat, and instruction viewer.
- Management UI should feel like a dense operational SaaS tool: efficient, scannable, and audit-friendly.
- Chat UI should prioritize fast question asking, citations, answer feedback, and safe links to the instruction viewer.
- Instruction viewer UI should prioritize readable instruction content, access validation states, and clear token expiration handling.
- Avoid landing-page composition, marketing hero sections, decorative cards, and visually noisy gradients in product surfaces.
- Prefer compact tables, forms, filters, detail panels, tabs, dialogs, menus, badges, and status indicators using `shadcn/ui` patterns.
- Use `lucide-react` icons for actions when a familiar icon exists.
- Icon-only action buttons in management surfaces must expose the same text as accessible name and hover/focus tooltip.

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
- AI budget configuration is part of the users/groups workspace because the budget is configured per user and needs role/group context.
- User deactivation is a logical status change exposed from the users/groups table; inactive users remain visible for audit and recovery.
- Feedback review is a separate management workspace. Chat feedback remains audit evidence tied to RAG query audit rows, but it must not replace functional management audit activity.
- Active session identity and logout controls live at the bottom of the management sidebar, not in a top workspace bar.
- The audit workspace is reserved for functional management events and reads `.NET` `/api/audit/events`, backed by `app.audit_events`. Document lifecycle events are visible there, and local demo seeding with `-WithSampleInstruction` inserts a sample `instruction.created` event.
- Use table/list views for operational review workflows, with filters above or beside the result set.
- Use detail panels or pages for document lifecycle state, version history, audit events, indexing status, and feedback context.
- The document list must support filtering across the visible document attributes: search text, lifecycle state, indexing state, instruction type, audience, access-group coverage, and updated metadata when available.
- Forms must show field-level validation, server errors, dirty state, disabled submission state, and recovery actions.
- The document editor supports assisted PDF/DOCX import through the .NET API: upload, extraction loading state, extraction error state, extracted text inserted into the editor, and user-controlled formatting before save/review.
- The import upload control must show the 10 MB per-file limit and validate file size before upload when the browser exposes the size.
- If extraction fails because no text is extractable, the UI shows a clear error, keeps current editor content unchanged, and lets the user upload another file or enter content manually.
- If the user cancels or leaves without saving, the UI must treat the extracted text as unsaved editor state and discard it like any other unsaved draft changes.

## Feedback Review UI

- `Admin` and `DocumentManager` can access feedback review from the dedicated management feedback workspace.
- The feedback review view uses a dense table/list layout.
- Filters include feedback polarity, negative-only mode, cited document, user, and date range.
- Rows show question, answer summary, feedback value, optional comment, cited documents, user, timestamp, cache hit, and request ID.
- The empty state must distinguish no feedback exists yet from filters returning no matches.
- The view reads data through the .NET API; it must not call FastAPI directly.

## AI Usage Budget UI

- `Admin` can configure per-user monthly AI usage budgets from the management app.
- AI budget controls live in the users/groups workspace rather than a separate duplicated budget screen.
- The budget view should show user, role/groups context, current period spend, monthly budget, remaining budget, budget status, last usage timestamp, and actions to adjust or disable the limit.
- Budget values are monetary amounts, initially USD, and are distinct from request-frequency rate limits. The default monthly budget shown for new users is USD 5 unless configured otherwise.
- The budget UI must show the current customer calendar-month period according to the deployment timezone.
- Over-budget chat users must see a clear service-limited state that does not expose internal pricing logic or technical details.
- Budget exhaustion must not be presented as a general account lockout; users can still open authorized documents and use non-AI workflows.

## Chat UI

- Each answer exposes thumbs up/down feedback controls and an optional comment entry after the user chooses a feedback value.
- Feedback submission must show loading, success, retryable error, and already-submitted states. The same user can update their feedback on an answer in the MVP.
- Citation links open `docs.client.com` with scoped viewer access tokens.
- Citation links use one-time exchange-code URLs. If the exchange code has expired, was already used, or is unauthorized, the viewer shows a safe expired-link or access-denied state with navigation back to chat.

## Instruction Viewer UI

- The viewer must handle exchange-code loading, expired-code, already-used-code, unauthorized, token-expired, document-not-found, and successful document states.
- The real viewer access token must never be visible to JavaScript or shown in the URL.

## Management Document Editor

- The management document editor is a full workspace tab inside `Documentos`, not a modal, because draft creation/review is a complex workflow with metadata, access groups, import, validation, and rich editing.
- The management editor uses TipTap with the starter kit plus `Link`, `Image`, `Underline`, and table extensions.
- Output is HTML stored in `app.instruction_versions.content_html` after server-side sanitization with `Ganss.Xss`.
- The editor must support headings (h1–h3), bold/italic/underline, ordered/unordered lists, links, inline code, code blocks, and tables.
- Disallow raw `<script>`, `<iframe>`, `<style>`, inline `style` attributes (except sanitizer-approved), and `on*` handlers. The sanitizer strips these regardless of UI controls.
- The assisted PDF/DOCX import inserts extracted plain-text content as paragraph nodes; the user formats afterwards.

## Chat Streaming

- The chat frontend consumes Server-Sent Events from FastAPI to render the answer progressively.
- Streaming UI must display: typing indicator before the first token, progressive answer body, citations panel populated once the answer terminates, feedback controls enabled only after stream completion.
- If the SSE connection drops mid-stream, the UI shows a retryable error state. Partial answers are not committed to chat history.

## Open UI Decisions

- Responsive breakpoints.
- Detailed screen layouts for management, chat, and instruction viewer.
