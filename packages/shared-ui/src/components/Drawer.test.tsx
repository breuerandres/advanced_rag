import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { Drawer } from './Drawer';

describe('Drawer', () => {
  it('renders open complementary panel content', () => {
    render(<Drawer open title="Citas">Detalle</Drawer>);

    expect(screen.getByRole('dialog', { name: 'Citas' })).toBeInTheDocument();
  });
});
