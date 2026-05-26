import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { DataTable } from './DataTable';

describe('DataTable', () => {
  it('renders headers and rows', () => {
    render(
      <DataTable
        columns={[{ key: 'name', header: 'Nombre', render: (row) => row.name }]}
        data={[{ id: '1', name: 'Ana' }]}
        getRowId={(row) => row.id}
      />,
    );

    expect(screen.getByRole('columnheader', { name: 'Nombre' })).toBeInTheDocument();
    expect(screen.getByRole('cell', { name: 'Ana' })).toBeInTheDocument();
  });

  it('can render a configured column as the row header', () => {
    render(
      <DataTable
        columns={[{ key: 'name', header: 'Nombre', render: (row) => row.name, rowHeader: true }]}
        data={[{ id: '1', name: 'Ana' }]}
        getRowId={(row) => row.id}
      />,
    );

    expect(screen.getByRole('rowheader', { name: 'Ana' })).toBeInTheDocument();
  });
});
