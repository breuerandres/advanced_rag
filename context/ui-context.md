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

## Management Layout Patterns

- Use persistent navigation for management sections such as documents, users/groups, audit, feedback review, and configuration.
- Use table/list views for operational review workflows, with filters above or beside the result set.
- Use detail panels or pages for document lifecycle state, version history, audit events, indexing status, and feedback context.
- Forms must show field-level validation, server errors, dirty state, disabled submission state, and recovery actions.
- The document editor supports assisted PDF/DOCX import through the .NET API: upload, extraction loading state, extraction error state, extracted text inserted into the editor, and user-controlled formatting before save/review.
- The import upload control must show the 10 MB per-file limit and validate file size before upload when the browser exposes the size.
- If extraction fails because no text is extractable, the UI shows a clear error, keeps current editor content unchanged, and lets the user upload another file or enter content manually.
- If the user cancels or leaves without saving, the UI must treat the extracted text as unsaved editor state and discard it like any other unsaved draft changes.

## Feedback Review UI

- `Admin` and `DocumentManager` can access the feedback review view in `manage.client.com`.
- The feedback review view uses a dense table/list layout.
- Filters include feedback polarity, negative-only mode, cited document, user, and date range.
- Rows show question, answer summary, feedback value, optional comment, cited documents, user, timestamp, cache hit, and request ID.
- The empty state must distinguish no feedback exists yet from filters returning no matches.
- The view reads data through the .NET API; it must not call FastAPI directly.

## AI Usage Budget UI

- `Admin` can configure per-user monthly AI usage budgets from the management app.
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

- The management editor uses TipTap with the starter kit plus `Link` and `Image` extensions.
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
