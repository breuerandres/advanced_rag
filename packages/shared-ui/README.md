# @helpcenter/shared-ui

Internal design system consumed by `apps/manage-web`, `apps/chat-web`, and `apps/docs-web`.

See `docs/adr/0007-shared-ui-design-system.md` for the rationale.

## What's here

- `src/styles/tokens.css` - CSS variables for light and dark themes.
- `src/styles/fonts.css` - Inter via `@fontsource/inter`.
- `src/styles/globals.css` - base resets.
- `src/hooks/useTheme.ts` - dark mode and persistence.
- `src/hooks/useShortcut.ts` - keyboard shortcut binding.
- `src/lib/cn.ts` - `clsx`/`tailwind-merge` helper.
- Layout and controls: `AppShell`, `Header`, `Sidebar`, `Button`, `DarkModeToggle`, `LanguageSelect`, `CommandPalette`.
- Forms: `Input`, `Textarea`, `Select`, `Checkbox`, `RadioGroup`, `Switch`.
- Overlays: `Dialog`, `Drawer`, `HoverCard`, `Tooltip`, `Popover`, `DropdownMenu`.
- Data: `DataTable` (TanStack Table wrapper), `Pagination`, `Badge`, `Avatar`.
- Feedback: `ToastViewport`/`notify`, `Skeleton`, `EmptyState`.
- Content and chat: `Markdown`, `ChatMessage`, `ChatComposer`, `ConversationList`, `CitationCard`, `CitationDrawer`.
- `src/index.ts` - barrel exports.

## What's pending

Phase 1.5.5 component primitives are implemented. Future hardening work:

- Apply shared UI data and overlay primitives to the three SPAs during Phase 1.5.6/1.7.
- Add Storybook when the component API stabilizes.

Each component must continue to:

1. Live in `src/components/<Name>.tsx`.
2. Be built on Radix UI primitives where applicable.
3. Use design tokens from `styles/tokens.css`.
4. Support keyboard nav, visible focus, and WCAG AA contrast in both themes.
5. Have a colocated `<Name>.test.tsx` when new behavior is added.
6. Be exported through `src/index.ts`.

## Consuming from an app

In each app's `src/main.tsx`:

```tsx
import '@helpcenter/shared-ui/styles/fonts.css';
import '@helpcenter/shared-ui/styles/tokens.css';
import '@helpcenter/shared-ui/styles/globals.css';
import './index.css';
```

And then:

```tsx
import { AppShell, Header, Sidebar, Button, useTheme } from '@helpcenter/shared-ui';
```

## Tailwind source scanning

Each app must include the shared package source in Tailwind's scan list. The current apps
use Tailwind CSS v4 with an app-local CSS `@source` directive:

```css
@import "tailwindcss";
@source "../../../packages/shared-ui/src";
```
