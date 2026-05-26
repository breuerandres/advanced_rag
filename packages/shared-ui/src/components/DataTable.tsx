import { type ReactNode } from 'react';
import {
  flexRender,
  getCoreRowModel,
  useReactTable,
  type ColumnDef,
} from '@tanstack/react-table';
import { cn } from '../lib/cn';

export interface DataTableColumn<T> {
  key: string;
  header: ReactNode;
  render: (row: T) => ReactNode;
  className?: string;
  rowHeader?: boolean;
}

function getColumnRowHeader(meta: unknown): boolean {
  const candidate = meta as { rowHeader?: unknown } | undefined;
  return candidate?.rowHeader === true;
}

export interface DataTableProps<T> {
  columns: DataTableColumn<T>[];
  data: T[];
  getRowId: (row: T) => string;
  emptyState?: ReactNode;
  className?: string;
}

function getColumnClassName(meta: unknown): string | undefined {
  const candidate = meta as { className?: unknown } | undefined;
  return typeof candidate?.className === 'string' ? candidate.className : undefined;
}

export function DataTable<T>({ columns, data, getRowId, emptyState, className }: DataTableProps<T>) {
  const tableColumns: Array<ColumnDef<T, ReactNode>> = columns.map((column) => ({
    id: column.key,
    header: () => column.header,
    cell: (context) => column.render(context.row.original),
    meta: { className: column.className, rowHeader: column.rowHeader },
  }));

  const table = useReactTable({
    data,
    columns: tableColumns,
    getRowId,
    getCoreRowModel: getCoreRowModel(),
  });

  if (data.length === 0 && emptyState) {
    return <>{emptyState}</>;
  }

  return (
    <div className={cn('overflow-auto rounded-[var(--radius)] border border-[var(--border)] bg-[var(--bg-elevated)]', className)}>
      <table className="w-full border-collapse text-left text-sm">
        <thead className="bg-[var(--bg-subtle)] text-xs font-semibold uppercase tracking-[var(--tracking-wide)] text-[var(--fg-muted)]">
          {table.getHeaderGroups().map((headerGroup) => (
            <tr key={headerGroup.id}>
              {headerGroup.headers.map((header) => (
                <th
                  key={header.id}
                  scope="col"
                  className={cn(
                    'border-b border-[var(--border)] px-3 py-2',
                    getColumnClassName(header.column.columnDef.meta),
                  )}
                >
                  {header.isPlaceholder ? null : flexRender(header.column.columnDef.header, header.getContext())}
                </th>
              ))}
            </tr>
          ))}
        </thead>
        <tbody>
          {table.getRowModel().rows.map((row) => (
            <tr key={row.id} className="border-b border-[var(--border)] last:border-b-0">
              {row.getVisibleCells().map((cell) => {
                const cellClassName = cn(
                  'px-3 py-2 text-[var(--fg)]',
                  getColumnClassName(cell.column.columnDef.meta),
                );
                const content = flexRender(cell.column.columnDef.cell, cell.getContext());

                return getColumnRowHeader(cell.column.columnDef.meta) ? (
                  <th key={cell.id} scope="row" className={cellClassName}>
                    {content}
                  </th>
                ) : (
                  <td key={cell.id} className={cellClassName}>
                    {content}
                  </td>
                );
              })}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
