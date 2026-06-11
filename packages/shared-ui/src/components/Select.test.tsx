import { render, screen, within } from '@testing-library/react';
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

  it('renders option badges and indentation, and badges the selected value in the trigger', async () => {
    render(
      <Select
        ariaLabel="Unidad"
        value="child"
        onValueChange={vi.fn()}
        options={[
          { value: 'root', label: 'Empresa', depth: 0, badge: <span aria-hidden>N0</span> },
          { value: 'child', label: 'Ventas', depth: 1, badge: <span aria-hidden>N1</span> },
        ]}
      />,
    );

    // The selected option's badge shows in the closed trigger; the aria-hidden
    // badge does not pollute the combobox accessible name.
    expect(screen.getByText('N1')).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Unidad' })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('combobox', { name: 'Unidad' }));

    const rootOption = await screen.findByRole('option', { name: 'Empresa' });
    const childOption = screen.getByRole('option', { name: 'Ventas' });
    expect(within(rootOption).getByText('N0')).toBeInTheDocument();
    expect(within(childOption).getByText('N1')).toBeInTheDocument();
    // depth 0 adds no inline indent; depth 1 is indented past the base padding
    // (jsdom collapses `calc(2rem + 0.75rem)` to `calc(2.75rem)`).
    expect(rootOption.style.paddingLeft).toBe('');
    expect(childOption.style.paddingLeft).toBe('calc(2.75rem)');
  });
});
