import * as TooltipPrimitive from '@radix-ui/react-tooltip';
import { type ReactNode } from 'react';

export interface TooltipProps {
  content: ReactNode;
  children: ReactNode;
}

export function Tooltip({ content, children }: TooltipProps) {
  return (
    <TooltipPrimitive.Provider delayDuration={0}>
      <TooltipPrimitive.Root>
        <TooltipPrimitive.Trigger asChild>{children}</TooltipPrimitive.Trigger>
        <TooltipPrimitive.Portal>
          <TooltipPrimitive.Content
            role="tooltip"
            sideOffset={6}
            className="z-[var(--z-popover)] rounded-[var(--radius)] border border-[var(--border)] bg-[var(--bg-inverse)] px-2 py-1 text-xs text-[var(--fg-inverse)] shadow-[var(--shadow-lg)]"
          >
            {content}
            <TooltipPrimitive.Arrow className="fill-[var(--bg-inverse)]" />
          </TooltipPrimitive.Content>
        </TooltipPrimitive.Portal>
      </TooltipPrimitive.Root>
    </TooltipPrimitive.Provider>
  );
}
