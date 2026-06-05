import { type ChangeEvent, type FormEvent, useLayoutEffect, useRef, useState } from 'react';
import { Send } from 'lucide-react';
import { Button } from './Button';
import { Textarea } from './Textarea';

export interface ChatComposerProps {
  onSubmit: (question: string) => void;
  disabled?: boolean;
  placeholder?: string;
  maxLength?: number;
  submitLabel?: string;
  pendingLabel?: string;
  characterCountLabel?: (count: number, maxLength: number) => string;
}

export function ChatComposer({
  onSubmit,
  disabled,
  placeholder = 'Escribi tu pregunta',
  maxLength,
  submitLabel = 'Enviar',
  pendingLabel,
  characterCountLabel,
}: ChatComposerProps) {
  const [value, setValue] = useState('');
  const textareaRef = useRef<HTMLTextAreaElement | null>(null);
  const normalizedValue = value.trim();

  useLayoutEffect(() => {
    const textarea = textareaRef.current;
    if (!textarea) {
      return;
    }

    textarea.style.height = 'auto';
    textarea.style.height = `${Math.min(textarea.scrollHeight, 160)}px`;
  }, [value]);

  function handleChange(event: ChangeEvent<HTMLTextAreaElement>) {
    setValue(event.currentTarget.value);
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!normalizedValue) {
      return;
    }
    onSubmit(normalizedValue);
    setValue('');
  }

  return (
    <form className="grid gap-3" onSubmit={handleSubmit}>
      <Textarea
        ref={textareaRef}
        aria-label="Pregunta"
        value={value}
        disabled={disabled}
        placeholder={placeholder}
        rows={1}
        className="chat-composer-textarea min-h-[2.75rem] max-h-40 resize-none overflow-y-auto"
        maxLength={maxLength}
        onChange={handleChange}
      />
      <div className="flex items-center justify-between gap-3 text-xs text-[var(--fg-muted)]">
        {maxLength && characterCountLabel ? (
          <span>{characterCountLabel(value.length, maxLength)}</span>
        ) : (
          <span />
        )}
        <Button type="submit" disabled={disabled || !normalizedValue} aria-label={disabled && pendingLabel ? pendingLabel : submitLabel}>
          <Send className="h-4 w-4" aria-hidden="true" />
          <span>{disabled && pendingLabel ? pendingLabel : submitLabel}</span>
        </Button>
      </div>
    </form>
  );
}
