import * as DialogPrimitive from '@radix-ui/react-dialog';
import { type ReactNode } from 'react';
import { cn } from '../lib/cn';

export interface DialogProps extends DialogPrimitive.DialogProps {
  title: string;
  description?: string;
  trigger?: ReactNode;
  footer?: ReactNode;
  children: ReactNode;
  className?: string;
}

export function Dialog({ title, description, trigger, footer, children, className, ...props }: DialogProps) {
  return (
    <DialogPrimitive.Root {...props}>
      {trigger && <DialogPrimitive.Trigger asChild>{trigger}</DialogPrimitive.Trigger>}
      <DialogPrimitive.Portal>
        <DialogPrimitive.Overlay className="fixed inset-0 z-[var(--z-modal)] bg-black/35" />
        <DialogPrimitive.Content
          className={cn(
            'fixed left-1/2 top-1/2 z-[var(--z-modal)] grid max-h-[85vh] w-[min(92vw,34rem)] -translate-x-1/2 -translate-y-1/2 gap-4 overflow-auto rounded-[var(--radius-lg)] border border-[var(--border)] bg-[var(--bg-elevated)] p-5 text-[var(--fg)] shadow-[var(--shadow-xl)]',
            className,
          )}
        >
          <div>
            <DialogPrimitive.Title className="text-base font-semibold">{title}</DialogPrimitive.Title>
            {description && (
              <DialogPrimitive.Description className="mt-1 text-sm text-[var(--fg-muted)]">
                {description}
              </DialogPrimitive.Description>
            )}
          </div>
          <div>{children}</div>
          {footer && <div className="flex justify-end gap-2 border-t border-[var(--border)] pt-4">{footer}</div>}
        </DialogPrimitive.Content>
      </DialogPrimitive.Portal>
    </DialogPrimitive.Root>
  );
}
