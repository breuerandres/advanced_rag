import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { Textarea } from './Textarea';

describe('Textarea', () => {
  it('renders an accessible multiline field', () => {
    render(<Textarea aria-label="Comment" defaultValue="Ready" />);

    expect(screen.getByRole('textbox', { name: 'Comment' })).toHaveValue('Ready');
  });
});
