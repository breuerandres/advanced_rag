import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { Input } from './Input';

describe('Input', () => {
  it('renders an accessible input with invalid state', () => {
    render(<Input aria-label="Email" invalid defaultValue="ana@example.com" />);

    expect(screen.getByRole('textbox', { name: 'Email' })).toHaveAttribute('aria-invalid', 'true');
  });
});
