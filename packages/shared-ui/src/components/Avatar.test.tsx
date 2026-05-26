import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { Avatar } from './Avatar';

describe('Avatar', () => {
  it('falls back to initials', () => {
    render(<Avatar name="Ana Gomez" />);

    expect(screen.getByLabelText('Ana Gomez')).toHaveTextContent('AG');
  });
});
