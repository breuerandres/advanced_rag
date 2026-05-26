import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { Markdown } from './Markdown';

describe('Markdown', () => {
  it('renders sanitized markdown content', () => {
    render(<Markdown content={'# Titulo\n\n<img src=x onerror=alert(1) />\n\nTexto'} />);

    expect(screen.getByRole('heading', { name: 'Titulo' })).toBeInTheDocument();
    expect(screen.getByText('Texto')).toBeInTheDocument();
    expect(screen.queryByRole('img')).not.toBeInTheDocument();
  });
});
