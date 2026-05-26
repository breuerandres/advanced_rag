import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { CitationCard } from './CitationCard';

describe('CitationCard', () => {
  it('renders citation document and heading', () => {
    render(<CitationCard title="Politica" headingPath={['Seguridad', 'Acceso']} />);

    expect(screen.getByText('Politica')).toBeInTheDocument();
    expect(screen.getByText('Seguridad / Acceso')).toBeInTheDocument();
  });
});
