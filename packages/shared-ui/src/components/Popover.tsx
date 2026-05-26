import * as PopoverPrimitive from '@radix-ui/react-popover';
import { type ReactNode } from 'react';

export interface PopoverProps {
  trigger: ReactNode;
  children: ReactNode;
}

export function Popover({ trigger, children }: PopoverProps) {
  return (
    <PopoverPrimitive.Root>
      <PopoverPrimitive.Trigger asChild>{trigger}</PopoverPrimitive.Trigger>
      <PopoverPrimitive.Portal>
        <PopoverPrimitive.Content
          sideOffset={6}
          className="z-[var(--z-popover)] min-w-56 rounded-[var(--radius)] border border-[var(--border)] bg-[var(--bg-elevated)] p-3 text-sm text-[var(--fg)] shadow-[var(--shadow-lg)]"
        >
          {children}
        </PopoverPrimitive.Content>
      </PopoverPrimitive.Portal>
    </PopoverPrimitive.Root>
  );
}
