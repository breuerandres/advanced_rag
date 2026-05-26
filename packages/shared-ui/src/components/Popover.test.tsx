import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { Popover } from './Popover';

describe('Popover', () => {
  it('opens popover content from its trigger', async () => {
    render(<Popover trigger={<button>Filtros</button>}>Estado</Popover>);

    await userEvent.click(screen.getByRole('button', { name: 'Filtros' }));

    expect(await screen.findByText('Estado')).toBeInTheDocument();
  });
});
