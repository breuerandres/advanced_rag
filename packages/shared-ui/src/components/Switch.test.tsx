import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { Switch } from './Switch';

describe('Switch', () => {
  it('toggles with an accessible name', async () => {
    const onCheckedChange = vi.fn();
    render(<Switch label="Modo oscuro" onCheckedChange={onCheckedChange} />);

    await userEvent.click(screen.getByRole('switch', { name: 'Modo oscuro' }));

    expect(onCheckedChange).toHaveBeenCalledWith(true);
  });
});
