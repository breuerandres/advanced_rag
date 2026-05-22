import { Command } from 'cmdk';
import { type ReactNode, useEffect, useState } from 'react';
import { useShortcut } from '../hooks/useShortcut';
import { cn } from '../lib/cn';

export interface CommandItem {
  /** Stable id, used as React key. */
  id: string;
  /** Visible label. */
  label: string;
  /** Optional short description shown faintly to the right. */
  hint?: string;
  /** Icon component (lucide). */
  icon?: ReactNode;
  /** Called when selected. */
  onSelect: () => void;
  /** Keyboard shortcut visualised next to the item, purely decorative. */
  shortcut?: string;
}

export interface CommandGroup {
  /** Visible heading for the group. */
  heading: string;
  items: CommandItem[];
}

interface CommandPaletteProps {
  /** Groups of commands to display. Items inside each group keep their order. */
  groups: CommandGroup[];
  /** Optional placeholder text. */
  placeholder?: string;
  /** Whether to bind Cmd+K / Ctrl+K to open the palette. Default true. */
  bindShortcut?: boolean;
}

/**
 * Linear/Vercel-style command palette. Open with Cmd+K / Ctrl+K (or controlled mode
 * via `open`/`onOpenChange` in a future iteration; first version is uncontrolled).
 */
export function CommandPalette({
  groups,
  placeholder = 'Search…',
  bindShortcut = true,
}: CommandPaletteProps) {
  const [open, setOpen] = useState(false);

  useShortcut(
    { key: 'k', mods: ['Meta'] },
    () => setOpen((v) => !v),
  );
  // Also bind Escape to close.
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(false);
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [open]);

  if (!bindShortcut) {
    // For now we always bind. Future: respect bindShortcut=false for controlled mode.
  }

  if (!open) return null;

  return (
    <div
      className="fixed inset-0 z-[var(--z-cmdk)] flex items-start justify-center bg-black/30 p-4 pt-[15vh] backdrop-blur-sm"
      onClick={(e) => {
        if (e.target === e.currentTarget) setOpen(false);
      }}
      role="presentation"
    >
      <Command
        label="Command palette"
        className={cn(
          'w-full max-w-xl overflow-hidden rounded-[var(--radius-lg)]',
          'border border-[var(--border)] bg-[var(--bg-elevated)] shadow-[var(--shadow-xl)]',
        )}
      >
        <Command.Input
          placeholder={placeholder}
          className={cn(
            'w-full border-b border-[var(--border)] bg-transparent px-4 py-3',
            'text-sm text-[var(--fg)] placeholder:text-[var(--fg-subtle)]',
            'focus:outline-none',
          )}
          autoFocus
        />
        <Command.List className="max-h-[60vh] overflow-y-auto p-2">
          <Command.Empty className="px-4 py-6 text-center text-sm text-[var(--fg-muted)]">
            No results.
          </Command.Empty>
          {groups.map((group) => (
            <Command.Group
              key={group.heading}
              heading={group.heading}
              className="mb-2 [&_[cmdk-group-heading]]:px-2 [&_[cmdk-group-heading]]:py-1.5 [&_[cmdk-group-heading]]:text-xs [&_[cmdk-group-heading]]:font-medium [&_[cmdk-group-heading]]:text-[var(--fg-muted)]"
            >
              {group.items.map((item) => (
                <Command.Item
                  key={item.id}
                  value={`${group.heading}:${item.label}:${item.hint ?? ''}`}
                  onSelect={() => {
                    setOpen(false);
                    item.onSelect();
                  }}
                  className={cn(
                    'flex cursor-pointer items-center gap-2 rounded-[var(--radius-sm)] px-2 py-1.5 text-sm',
                    'text-[var(--fg)]',
                    'data-[selected=true]:bg-[var(--bg-subtle)]',
                    'transition-colors',
                  )}
                >
                  {item.icon && (
                    <span className="text-[var(--fg-muted)]">{item.icon}</span>
                  )}
                  <span className="flex-1">{item.label}</span>
                  {item.hint && (
                    <span className="text-xs text-[var(--fg-subtle)]">{item.hint}</span>
                  )}
                  {item.shortcut && (
                    <kbd className="ml-2 hidden rounded border border-[var(--border)] bg-[var(--bg-subtle)] px-1.5 py-0.5 text-[10px] text-[var(--fg-muted)] sm:inline-block">
                      {item.shortcut}
                    </kbd>
                  )}
                </Command.Item>
              ))}
            </Command.Group>
          ))}
        </Command.List>
      </Command>
    </div>
  );
}
