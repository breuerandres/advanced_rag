import { render, screen } from '@testing-library/react';
import { describe, expect, test } from 'vitest';
import { Button } from './Button';

describe('Button', () => {
  test('keeps icon button accessible tooltip metadata without duplicating the browser title', () => {
    render(
      <Button className="icon-button" aria-label="Actualizar documentos" title="Actualizar">
        X
      </Button>,
    );

    const button = screen.getByRole('button', { name: 'Actualizar documentos' });
    expect(button).toHaveAttribute('data-tooltip', 'Actualizar documentos');
    expect(button).not.toHaveAttribute('title');
  });
});
