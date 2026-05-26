import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { Checkbox } from './Checkbox';

describe('Checkbox', () => {
  it('toggles from the labeled control', async () => {
    const onCheckedChange = vi.fn();
    render(<Checkbox label="Activo" onCheckedChange={onCheckedChange} />);

    await userEvent.click(screen.getByRole('checkbox', { name: 'Activo' }));

    expect(onCheckedChange).toHaveBeenCalledWith(true);
  });
});
