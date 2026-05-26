import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { RadioGroup } from './RadioGroup';

describe('RadioGroup', () => {
  it('selects an option by accessible label', async () => {
    const onValueChange = vi.fn();
    render(
      <RadioGroup
        ariaLabel="Rol"
        options={[
          { value: 'admin', label: 'Admin' },
          { value: 'viewer', label: 'Viewer' },
        ]}
        onValueChange={onValueChange}
      />,
    );

    await userEvent.click(screen.getByRole('radio', { name: 'Viewer' }));

    expect(onValueChange).toHaveBeenCalledWith('viewer');
  });
});
