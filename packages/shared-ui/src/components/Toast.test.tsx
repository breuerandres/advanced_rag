import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { ToastViewport, notify } from './Toast';

describe('Toast', () => {
  it('renders toast notifications', async () => {
    render(<ToastViewport />);

    notify.success('Guardado');

    expect(await screen.findByText('Guardado')).toBeInTheDocument();
  });
});
