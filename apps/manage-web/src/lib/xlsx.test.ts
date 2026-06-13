import { describe, expect, it } from 'vitest'
import ExcelJS from 'exceljs'
import { buildXlsxBuffer } from './xlsx'

interface SampleRow {
  name: string
  score: number
  when: Date
  comment: string | null
}

async function loadFirstSheet(buffer: ArrayBuffer, sheetName: string) {
  const workbook = new ExcelJS.Workbook()
  await workbook.xlsx.load(buffer)
  const sheet = workbook.getWorksheet(sheetName)
  if (!sheet) {
    throw new Error(`sheet ${sheetName} not found`)
  }
  return sheet
}

describe('buildXlsxBuffer', () => {
  it('writes localized headers and row values matching the columns', async () => {
    const when = new Date('2026-05-18T11:30:00Z')
    const buffer = await buildXlsxBuffer<SampleRow>({
      sheetName: 'Muestra',
      columns: [
        { header: 'Nombre', value: (row) => row.name },
        { header: 'Puntaje', value: (row) => row.score },
        { header: 'Fecha', value: (row) => row.when, numFmt: 'dd/mm/yyyy hh:mm' },
        { header: 'Comentario', value: (row) => row.comment },
      ],
      rows: [{ name: 'Ada', score: 42, when, comment: null }],
    })

    const sheet = await loadFirstSheet(buffer, 'Muestra')

    expect(sheet.getRow(1).getCell(1).value).toBe('Nombre')
    expect(sheet.getRow(1).getCell(2).value).toBe('Puntaje')
    expect(sheet.getRow(1).getCell(3).value).toBe('Fecha')
    expect(sheet.getRow(1).getCell(4).value).toBe('Comentario')
    expect(sheet.getRow(1).getCell(1).font?.bold).toBe(true)

    const dataRow = sheet.getRow(2)
    expect(dataRow.getCell(1).value).toBe('Ada')
    expect(dataRow.getCell(2).value).toBe(42)
    expect(dataRow.getCell(3).value).toBeInstanceOf(Date)
    // null/undefined cell values are rendered as empty strings, never literal "null".
    expect(dataRow.getCell(4).value).toBe('')
  })

  it('freezes the header row and enables column filters', async () => {
    const buffer = await buildXlsxBuffer<{ a: string }>({
      sheetName: 'Filtros',
      columns: [
        { header: 'A', value: (row) => row.a },
        { header: 'B', value: () => 'b' },
      ],
      rows: [{ a: 'x' }],
    })

    const sheet = await loadFirstSheet(buffer, 'Filtros')

    expect(sheet.views[0]).toMatchObject({ state: 'frozen', ySplit: 1 })
    expect(sheet.autoFilter).toBeTruthy()
  })
})
