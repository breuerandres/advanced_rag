import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { Pagination } from './Pagination';

describe('Pagination', () => {
  it('moves to the next page', async () => {
    const onPageChange = vi.fn();
    render(<Pagination page={1} pageCount={3} onPageChange={onPageChange} />);

    await userEvent.click(screen.getByRole('button', { name: 'Pagina siguiente' }));

    expect(onPageChange).toHaveBeenCalledWith(2);
  });
});
