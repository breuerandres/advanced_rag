import * as SwitchPrimitive from '@radix-ui/react-switch';
import { useId } from 'react';
import { cn } from '../lib/cn';

export interface SwitchProps extends SwitchPrimitive.SwitchProps {
  label: string;
}

export function Switch({ id, label, className, ...props }: SwitchProps) {
  const generatedId = useId();
  const switchId = id ?? generatedId;

  return (
    <label className={cn('inline-flex items-center gap-2 text-sm text-[var(--fg)]', className)} htmlFor={switchId}>
      <SwitchPrimitive.Root
        id={switchId}
        className="relative h-5 w-9 rounded-[var(--radius-full)] bg-[var(--bg-muted)] transition-colors data-[state=checked]:bg-[var(--accent)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--border-focus)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--bg)]"
        {...props}
      >
        <SwitchPrimitive.Thumb className="block h-4 w-4 translate-x-0.5 rounded-[var(--radius-full)] bg-white shadow-[var(--shadow-sm)] transition-transform data-[state=checked]:translate-x-4" />
      </SwitchPrimitive.Root>
      <span>{label}</span>
    </label>
  );
}
