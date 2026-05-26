import * as HoverCardPrimitive from '@radix-ui/react-hover-card';
import { type ReactNode } from 'react';

export interface HoverCardProps {
  trigger: ReactNode;
  children: ReactNode;
}

export function HoverCard({ trigger, children }: HoverCardProps) {
  return (
    <HoverCardPrimitive.Root openDelay={0} closeDelay={0}>
      <HoverCardPrimitive.Trigger asChild>{trigger}</HoverCardPrimitive.Trigger>
      <HoverCardPrimitive.Portal>
        <HoverCardPrimitive.Content
          sideOffset={6}
          className="z-[var(--z-popover)] max-w-sm rounded-[var(--radius)] border border-[var(--border)] bg-[var(--bg-elevated)] p-3 text-sm text-[var(--fg)] shadow-[var(--shadow-lg)]"
        >
          {children}
        </HoverCardPrimitive.Content>
      </HoverCardPrimitive.Portal>
    </HoverCardPrimitive.Root>
  );
}
