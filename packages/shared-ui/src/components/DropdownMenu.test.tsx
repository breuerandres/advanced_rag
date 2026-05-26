import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { DropdownMenu } from './DropdownMenu';

describe('DropdownMenu', () => {
  it('invokes a menu item action', async () => {
    const onSelect = vi.fn();
    render(<DropdownMenu trigger={<button>Acciones</button>} items={[{ label: 'Editar', onSelect }]} />);

    await userEvent.click(screen.getByRole('button', { name: 'Acciones' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Editar' }));

    expect(onSelect).toHaveBeenCalled();
  });
});
