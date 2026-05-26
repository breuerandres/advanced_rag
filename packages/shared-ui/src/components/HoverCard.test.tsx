import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { HoverCard } from './HoverCard';

describe('HoverCard', () => {
  it('shows hover card content', async () => {
    render(<HoverCard trigger={<button>Info</button>}>Detalle</HoverCard>);

    await userEvent.hover(screen.getByRole('button', { name: 'Info' }));

    expect(await screen.findByText('Detalle')).toBeInTheDocument();
  });
});
