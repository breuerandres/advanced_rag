import { ChevronLeft, ChevronRight } from 'lucide-react';
import { Button } from './Button';

export interface PaginationProps {
  page: number;
  pageCount: number;
  onPageChange: (page: number) => void;
}

export function Pagination({ page, pageCount, onPageChange }: PaginationProps) {
  return (
    <nav className="flex items-center justify-between gap-3 text-sm text-[var(--fg-muted)]" aria-label="Paginacion">
      <Button
        type="button"
        variant="secondary"
        size="sm"
        aria-label="Pagina anterior"
        disabled={page <= 1}
        onClick={() => onPageChange(page - 1)}
      >
        <ChevronLeft className="h-4 w-4" aria-hidden="true" />
      </Button>
      <span className="tabular">
        {page} / {pageCount}
      </span>
      <Button
        type="button"
        variant="secondary"
        size="sm"
        aria-label="Pagina siguiente"
        disabled={page >= pageCount}
        onClick={() => onPageChange(page + 1)}
      >
        <ChevronRight className="h-4 w-4" aria-hidden="true" />
      </Button>
    </nav>
  );
}
