import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ChatComposer } from './ChatComposer';

describe('ChatComposer', () => {
  it('submits non-empty questions', async () => {
    const onSubmit = vi.fn();
    render(<ChatComposer onSubmit={onSubmit} />);

    await userEvent.type(screen.getByRole('textbox', { name: 'Pregunta' }), 'Que puedo hacer?');
    await userEvent.click(screen.getByRole('button', { name: 'Enviar' }));

    expect(onSubmit).toHaveBeenCalledWith('Que puedo hacer?');
  });

  it('supports custom labels, max length, and character count', async () => {
    const onSubmit = vi.fn();
    render(
      <ChatComposer
        onSubmit={onSubmit}
        maxLength={20}
        submitLabel="Enviar pregunta"
        pendingLabel="Enviando"
        characterCountLabel={(count, max) => `${count} / ${max}`}
      />,
    );

    const textbox = screen.getByRole('textbox', { name: 'Pregunta' });
    await userEvent.type(textbox, 'Como ingreso?');

    expect(textbox).toHaveAttribute('maxLength', '20');
    expect(screen.getByText('13 / 20')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Enviar pregunta' })).toBeEnabled();

    await userEvent.click(screen.getByRole('button', { name: 'Enviar pregunta' }));

    expect(onSubmit).toHaveBeenCalledWith('Como ingreso?');
    expect(screen.getByText('0 / 20')).toBeInTheDocument();
  });

  it('starts with a single-line autosizing textarea', () => {
    render(<ChatComposer onSubmit={vi.fn()} />);

    const textbox = screen.getByRole('textbox', { name: 'Pregunta' });

    expect(textbox).toHaveAttribute('rows', '1');
    expect(textbox).toHaveClass('chat-composer-textarea');
  });
});
