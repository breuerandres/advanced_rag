import { cn } from '../lib/cn';

export interface ConversationListItem {
  id: string;
  title: string;
  active?: boolean;
}

export interface ConversationListProps {
  items: ConversationListItem[];
  onSelect?: (id: string) => void;
  className?: string;
}

export function ConversationList({ items, onSelect, className }: ConversationListProps) {
  return (
    <nav aria-label="Conversaciones" className={cn('grid gap-1', className)}>
      {items.map((item) => (
        <button
          key={item.id}
          type="button"
          aria-current={item.active ? 'page' : undefined}
          className="rounded-[var(--radius)] px-3 py-2 text-left text-sm text-[var(--fg)] hover:bg-[var(--bg-subtle)] aria-[current=page]:bg-[var(--bg-muted)]"
          onClick={() => onSelect?.(item.id)}
        >
          {item.title}
        </button>
      ))}
    </nav>
  );
}
