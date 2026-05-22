# @helpcenter/shared-ui

Internal design system consumed by `apps/manage-web`, `apps/chat-web`, and `apps/docs-web`.

See `docs/adr/0007-shared-ui-design-system.md` for the rationale.

## What's here (initial scaffold)

- `src/styles/tokens.css` — CSS variables for light + dark themes
- `src/styles/fonts.css` — Inter via `@fontsource/inter`
- `src/styles/globals.css` — base resets
- `src/hooks/useTheme.ts` — dark mode + persistence
- `src/hooks/useShortcut.ts` — keyboard shortcut binding
- `src/lib/cn.ts` — `clsx`/`tailwind-merge` helper
- `src/components/Button.tsx` — primary, secondary, ghost, danger variants
- `src/components/AppShell.tsx` — sidebar + main + optional right panel layout
- `src/components/Header.tsx` — top header with logo, theme toggle, avatar slot
- `src/components/Sidebar.tsx` — collapsible left sidebar
- `src/components/DarkModeToggle.tsx` — visible toggle component
- `src/components/CommandPalette.tsx` — cmdk-backed command palette
- `src/index.ts` — barrel exports

## What's pending

Per `docs/v2/03-phases.md` §Phase 1.5.5:

- Inputs: `Input`, `Textarea`, `Select`, `Checkbox`, `RadioGroup`, `Switch`
- Overlays: `Dialog`, `Drawer`, `HoverCard`, `Tooltip`, `Popover`, `DropdownMenu`
- Data: `DataTable` (TanStack Table wrapper), `Pagination`, `Badge`, `Avatar`
- Feedback: `Toast` (Sonner), `Skeleton`, `EmptyState`
- Markdown: `Markdown` (react-markdown + rehype-sanitize)
- Chat: `ChatMessage`, `ChatComposer`, `ConversationList`, `CitationCard`, `CitationDrawer`

Each pending component must:

1. Live in `src/components/<Name>.tsx`.
2. Be built on Radix UI primitives where applicable.
3. Use design tokens from `styles/tokens.css`.
4. Support keyboard nav + visible focus + WCAG AA contrast in both themes.
5. Have a colocated `<Name>.test.tsx` (Vitest + @testing-library/react).
6. Be exported through `src/index.ts`.

## Consuming from an app

In each app's `src/main.tsx`:

```tsx
import '@helpcenter/shared-ui/styles/fonts.css';
import '@helpcenter/shared-ui/styles/tokens.css';
import '@helpcenter/shared-ui/styles/globals.css';
import './index.css';   // app-local Tailwind directives
```

And then:

```tsx
import { AppShell, Header, Sidebar, Button, useTheme } from '@helpcenter/shared-ui';
```

## Tailwind config

Each app's `tailwind.config.ts` must include the `shared-ui` source paths so its classes
get scanned:

```ts
export default {
  content: [
    './src/**/*.{ts,tsx}',
    '../../packages/shared-ui/src/**/*.{ts,tsx}',
  ],
  theme: {
    extend: {
      colors: {
        bg: 'var(--bg)',
        'bg-elevated': 'var(--bg-elevated)',
        fg: 'var(--fg)',
        'fg-muted': 'var(--fg-muted)',
        border: 'var(--border)',
        accent: 'var(--accent)',
      },
      fontFamily: {
        sans: ['Inter', 'system-ui', 'sans-serif'],
      },
    },
  },
};
```
