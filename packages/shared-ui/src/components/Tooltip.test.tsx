import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { Tooltip } from './Tooltip';

describe('Tooltip', () => {
  it('shows tooltip content on hover', async () => {
    render(<Tooltip content="Guardar cambios"><button>Guardar</button></Tooltip>);

    await userEvent.hover(screen.getByRole('button', { name: 'Guardar' }));

    const tooltips = await screen.findAllByRole('tooltip');
    expect(tooltips.some((tooltip) => tooltip.textContent === 'Guardar cambios')).toBe(true);
  });
});
