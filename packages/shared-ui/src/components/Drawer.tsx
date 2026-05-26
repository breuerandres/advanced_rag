import { type ReactNode } from 'react';
import { Dialog } from './Dialog';
import { cn } from '../lib/cn';

export interface DrawerProps {
  open?: boolean;
  defaultOpen?: boolean;
  onOpenChange?: (open: boolean) => void;
  title: string;
  description?: string;
  trigger?: ReactNode;
  children: ReactNode;
  className?: string;
}

export function Drawer({ className, ...props }: DrawerProps) {
  return (
    <Dialog
      {...props}
      className={cn(
        'left-auto right-0 top-0 h-screen max-h-screen w-[min(92vw,28rem)] translate-x-0 translate-y-0 rounded-none rounded-l-[var(--radius-lg)]',
        className,
      )}
    />
  );
}
