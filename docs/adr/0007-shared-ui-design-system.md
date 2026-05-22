# ADR-0007 — `packages/shared-ui` design system

**Status**: Accepted (v2 design phase, 2026-05-22)
**Phase**: 1.5

## Context

The MVP keeps three SPAs (`manage-web`, `chat-web`, `docs-web`) with independent UI code.
Each app re-implements Buttons, Inputs, Dialogs, theming, and has its own conventions for
spacing, colors, and shadows. The user requested two things this ADR addresses:

- A real visual refactor toward **Linear/Vercel-style minimalist professional**.
- Keep the three SPAs separate, but unify the look-and-feel.

## Decision

Create `packages/shared-ui` as a pnpm workspace package consumed by all three SPAs.

### Layout

```
packages/shared-ui/
├── package.json                    workspace package
├── src/
│   ├── index.ts                    barrel exports
│   ├── styles/
│   │   ├── tokens.css              CSS variables (light + dark)
│   │   ├── fonts.css               Inter via @fontsource
│   │   └── globals.css             base resets
│   ├── hooks/
│   │   ├── useTheme.ts             dark mode + persistence
│   │   ├── useShortcut.ts          keyboard shortcuts
│   │   ├── useStreamingFetch.ts    SSE consumer
│   │   ├── useApiClient.ts         shared fetch wrapper
│   │   └── useTenantConfig.ts      reads /api/v1/config
│   ├── lib/
│   │   ├── cn.ts                   class merge helper
│   │   └── parseApiError.ts        existing helper, moved here
│   └── components/                 see below
└── tsconfig.json
```

### Components (curated set)

- **Layout**: `AppShell`, `Sidebar`, `Header`, `Drawer`
- **Inputs**: `Button`, `Input`, `Textarea`, `Select`, `Checkbox`, `RadioGroup`, `Switch`
- **Overlays**: `Dialog`, `HoverCard`, `Tooltip`, `Popover`, `DropdownMenu`
- **Data**: `DataTable` (wraps TanStack Table), `Pagination`, `Badge`, `Avatar`
- **Feedback**: `Toast` (Sonner), `Skeleton`, `EmptyState`
- **Power**: `CommandPalette` (cmdk), `DarkModeToggle`
- **Chat**: `ChatMessage`, `ChatComposer`, `ConversationList`, `CitationCard`, `CitationDrawer`
- **Markdown**: `Markdown` (`react-markdown` + `rehype-sanitize`)

All built on Radix UI primitives where applicable. shadcn/ui copied components stay where
they are during the migration; we don't rip them out, we move them under `shared-ui`
component by component.

### Design tokens

CSS variables on `:root` (light) and `[data-theme='dark']` (dark). Tenant branding
overrides via `--brand-primary` set by the theme provider from `tenant_config`. Full token
list lives in `docs/v2/02-target-architecture.md` §6.

### Visual direction

- Typography: Inter (self-hosted via `@fontsource/inter`).
- Sizes: body 14–15px, line-height 1.5–1.65.
- Color: neutral grayscale + one tenant-configurable accent.
- Shadows: subtle (≤5% opacity, ≤10px blur).
- Animation: 120–320ms `cubic-bezier(0.16, 1, 0.3, 1)`; transform + opacity only.
- Spacing: 4px base unit.
- Borders: 8px default radius; 6px for chips, 12px for cards.

### Accessibility

WCAG AA minimum: contrast ≥ 4.5:1, visible focus rings, keyboard nav, aria-live for
streaming chat tokens.

## Alternatives considered

### Build UI per SPA, keep shadcn copies
Pros: No new package; familiar.
Cons: Three diverging implementations over time; the user explicitly asked for a unified
visual language.

### Bring in a third-party design system (Mantine, Chakra, Park UI, etc.)
Pros: Ready-to-go.
Cons: Heavy. Each has opinions that conflict with our token system. Switching design
direction (Linear/Vercel style) requires fighting the framework.

### Build a Storybook-first design system as a separate repo
Pros: True product-grade development practice.
Cons: Premature for a 6-phase refactor. Storybook is added as an optional dev dep, not as
a required workflow.

## Consequences

**Positive**
- One place to make a visual change; three SPAs pick it up.
- Reduced duplicate code across the three apps.
- Dark mode + branding + theming managed centrally.
- Component library can be Storybook-published later if needed.

**Negative**
- New build dependency graph (`packages/shared-ui` → three apps).
- pnpm workspace configuration must be correct or imports break (one-time hurdle).
- Migration cost: each existing component in the three SPAs must be re-pointed to
  `@helpcenter/shared-ui`.

**Risks / mitigations**
- The new package becomes a god-package with too many components → keep it small,
  feature-focused. If a component is only used in one SPA, leave it there.
- Bundle size grows for SPAs that don't use all components → Vite handles tree-shaking;
  package is ESM and uses named exports.

## References

- Linear's "Building Linear" essay (linear.app/blog/building-linear)
- Vercel design system (geist-ui.dev) — visual reference, not adopted as library
- Radix UI primitives (radix-ui.com)
- cmdk for command palette (cmdk.paco.me)
