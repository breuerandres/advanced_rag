import { Badge } from './Badge';
import { Markdown } from './Markdown';
import { cn } from '../lib/cn';

export type ChatMessageAuthor = 'user' | 'assistant' | 'system';

export interface ChatMessageProps {
  author: ChatMessageAuthor;
  content: string;
  pending?: boolean;
  /** Hide the author badge for transcript layouts where alignment already conveys the author. */
  showAuthor?: boolean;
  className?: string;
}

const labels: Record<ChatMessageAuthor, string> = {
  user: 'Mensaje del usuario',
  assistant: 'Mensaje del asistente',
  system: 'Mensaje del sistema',
};

const displayLabels: Record<ChatMessageAuthor, string> = {
  user: 'Usuario',
  assistant: 'Asistente',
  system: 'Sistema',
};

export function ChatMessage({ author, content, pending, showAuthor = true, className }: ChatMessageProps) {
  return (
    <article
      aria-label={labels[author]}
      className={cn(
        'rounded-[var(--radius-lg)] border border-[var(--border)] bg-[var(--bg-elevated)] p-4 text-sm shadow-[var(--shadow-xs)]',
        author === 'user' && 'ml-auto max-w-[80%] bg-[var(--bg-subtle)]',
        className,
      )}
    >
      {(showAuthor || pending) && (
        <div className="mb-2 flex items-center gap-2">
          {showAuthor && (
            <Badge tone={author === 'assistant' ? 'info' : 'neutral'}>{displayLabels[author]}</Badge>
          )}
          {pending && <span className="text-xs text-[var(--fg-muted)]">Escribiendo...</span>}
        </div>
      )}
      <Markdown content={content} />
      {pending && (
        <span
          aria-hidden="true"
          data-testid="streaming-cursor"
          className="mt-2 inline-block h-4 w-1 animate-pulse rounded-full bg-[var(--accent)] align-text-bottom"
        />
      )}
    </article>
  );
}
