import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { Select } from './Select';

describe('Select', () => {
  it('renders a keyboard-accessible select trigger and options', async () => {
    const onValueChange = vi.fn();
    render(
      <Select
        ariaLabel="Estado"
        placeholder="Seleccionar"
        options={[
          { value: 'draft', label: 'Borrador' },
          { value: 'published', label: 'Publicado' },
        ]}
        onValueChange={onValueChange}
      />,
    );

    await userEvent.click(screen.getByRole('combobox', { name: 'Estado' }));
    await userEvent.click(await screen.findByRole('option', { name: 'Publicado' }));

    expect(onValueChange).toHaveBeenCalledWith('published');
  });
});
