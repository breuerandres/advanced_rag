import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { ConversationList } from './ConversationList';

describe('ConversationList', () => {
  it('renders conversations as a navigation list', () => {
    render(<ConversationList items={[{ id: 'c1', title: 'Onboarding' }]} />);

    expect(screen.getByRole('navigation', { name: 'Conversaciones' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Onboarding' })).toBeInTheDocument();
  });
});
