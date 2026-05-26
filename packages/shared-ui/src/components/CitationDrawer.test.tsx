import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { CitationDrawer } from './CitationDrawer';

describe('CitationDrawer', () => {
  it('renders citation drawer content', () => {
    render(<CitationDrawer open citations={[{ id: '1', title: 'Politica' }]} />);

    expect(screen.getByRole('dialog', { name: 'Citas' })).toBeInTheDocument();
    expect(screen.getByText('Politica')).toBeInTheDocument();
  });
});
