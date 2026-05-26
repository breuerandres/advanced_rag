import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { ChatMessage } from './ChatMessage';

describe('ChatMessage', () => {
  it('renders assistant message with accessible label', () => {
    render(<ChatMessage author="assistant" content="Respuesta" />);

    expect(screen.getByLabelText('Mensaje del asistente')).toHaveTextContent('Respuesta');
    expect(screen.getByText('Asistente')).toBeInTheDocument();
    expect(screen.queryByText('assistant')).not.toBeInTheDocument();
  });
});
