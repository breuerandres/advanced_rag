// XLSX export helper shared by the management reporting screens (Feedback, Audit).
// The spreadsheet mirrors the on-screen table columns. exceljs is imported lazily so it
// stays out of the initial bundle and only loads when the user actually exports.

export interface XlsxColumn<TRow> {
  /** Localized column header, matching the on-screen table header. */
  header: string
  /** Extracts the cell value for a row. Returns a string, number or Date. */
  value: (row: TRow) => string | number | Date | null | undefined
  /** Column width in characters. Defaults to DEFAULT_COLUMN_WIDTH. */
  width?: number
  /** Excel number format, e.g. 'dd/mm/yyyy hh:mm' for date columns. */
  numFmt?: string
}

export interface XlsxExport<TRow> {
  sheetName: string
  columns: XlsxColumn<TRow>[]
  rows: TRow[]
}

const DEFAULT_COLUMN_WIDTH = 24

/**
 * Builds an XLSX file in memory from the given columns and rows. Extracted from the
 * download path so it can be unit-tested by reading the buffer back with exceljs.
 */
export async function buildXlsxBuffer<TRow>({
  sheetName,
  columns,
  rows,
}: XlsxExport<TRow>): Promise<ArrayBuffer> {
  // exceljs is CommonJS (Node entry) / UMD (browser entry). `default` is the module under
  // Vite's interop, while the namespace itself carries the named exports under Node/Vitest.
  type ExcelJsModule = typeof import('exceljs')
  const imported = (await import('exceljs')) as ExcelJsModule & { default?: ExcelJsModule }
  const ExcelJS = imported.default ?? imported

  const workbook = new ExcelJS.Workbook()
  const sheet = workbook.addWorksheet(sheetName)

  sheet.columns = columns.map((column) => ({
    header: column.header,
    width: column.width ?? DEFAULT_COLUMN_WIDTH,
    style: column.numFmt ? { numFmt: column.numFmt } : {},
  }))

  const headerRow = sheet.getRow(1)
  headerRow.font = { bold: true }

  for (const row of rows) {
    sheet.addRow(columns.map((column) => column.value(row) ?? ''))
  }

  // Freeze the header row and enable column filters, matching a clean reporting export.
  sheet.views = [{ state: 'frozen', ySplit: 1 }]
  sheet.autoFilter = {
    from: { row: 1, column: 1 },
    to: { row: 1, column: columns.length },
  }

  return workbook.xlsx.writeBuffer()
}

/** Builds the XLSX file and triggers a browser download. */
export async function exportRowsToXlsx<TRow>(
  fileName: string,
  exportData: XlsxExport<TRow>,
): Promise<void> {
  const buffer = await buildXlsxBuffer(exportData)
  const blob = new Blob([buffer], {
    type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  link.click()
  URL.revokeObjectURL(url)
}
