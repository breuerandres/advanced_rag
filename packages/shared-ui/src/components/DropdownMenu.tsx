import * as DropdownMenuPrimitive from '@radix-ui/react-dropdown-menu';
import { type ReactNode } from 'react';

export interface DropdownMenuItem {
  label: string;
  disabled?: boolean;
  onSelect?: () => void;
}

export interface DropdownMenuProps {
  trigger: ReactNode;
  items: DropdownMenuItem[];
}

export function DropdownMenu({ trigger, items }: DropdownMenuProps) {
  return (
    <DropdownMenuPrimitive.Root>
      <DropdownMenuPrimitive.Trigger asChild>{trigger}</DropdownMenuPrimitive.Trigger>
      <DropdownMenuPrimitive.Portal>
        <DropdownMenuPrimitive.Content
          sideOffset={6}
          className="z-[var(--z-popover)] min-w-48 rounded-[var(--radius)] border border-[var(--border)] bg-[var(--bg-elevated)] p-1 text-sm text-[var(--fg)] shadow-[var(--shadow-lg)]"
        >
          {items.map((item) => (
            <DropdownMenuPrimitive.Item
              key={item.label}
              {...(item.disabled !== undefined ? { disabled: item.disabled } : {})}
              {...(item.onSelect ? { onSelect: item.onSelect } : {})}
              className="cursor-default rounded-[var(--radius-sm)] px-2 py-1.5 outline-none data-[highlighted]:bg-[var(--bg-subtle)] data-[disabled]:opacity-50"
            >
              {item.label}
            </DropdownMenuPrimitive.Item>
          ))}
        </DropdownMenuPrimitive.Content>
      </DropdownMenuPrimitive.Portal>
    </DropdownMenuPrimitive.Root>
  );
}
