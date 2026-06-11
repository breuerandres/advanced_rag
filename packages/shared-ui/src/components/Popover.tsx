import * as PopoverPrimitive from '@radix-ui/react-popover';
import { type ReactNode } from 'react';
import { cn } from '../lib/cn';

export interface PopoverProps {
  trigger: ReactNode;
  children: ReactNode;
  /** Controlled open state. Omit for uncontrolled behavior. */
  open?: boolean;
  onOpenChange?: (open: boolean) => void;
  align?: 'start' | 'center' | 'end';
  /** Extra classes merged onto the content surface (e.g. width, max-height). */
  contentClassName?: string;
}

export function Popover({
  trigger,
  children,
  open,
  onOpenChange,
  align = 'center',
  contentClassName,
}: PopoverProps) {
  const rootProps = {
    ...(open !== undefined ? { open } : {}),
    ...(onOpenChange ? { onOpenChange } : {}),
  };

  return (
    <PopoverPrimitive.Root {...rootProps}>
      <PopoverPrimitive.Trigger asChild>{trigger}</PopoverPrimitive.Trigger>
      <PopoverPrimitive.Portal>
        <PopoverPrimitive.Content
          align={align}
          sideOffset={6}
          className={cn(
            'z-[var(--z-popover)] min-w-56 rounded-[var(--radius)] border border-[var(--border)] bg-[var(--bg-elevated)] p-3 text-sm text-[var(--fg)] shadow-[var(--shadow-lg)]',
            contentClassName,
          )}
        >
          {children}
        </PopoverPrimitive.Content>
      </PopoverPrimitive.Portal>
    </PopoverPrimitive.Root>
  );
}
