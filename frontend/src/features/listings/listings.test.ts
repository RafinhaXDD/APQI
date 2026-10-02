import { describe, expect, it } from 'vitest'
import { statusBadgeClass } from './components'
import { isBookBarcode, normalizeIsbn } from './isbn'

describe('normalizeIsbn (mirrors the server)', () => {
  it.each([
    ['9788535914849', '9788535914849'],
    ['978-85-359-1484-9', '9788535914849'],
    ['0-306-40615-2', '9780306406157'],
    ['080442957x', '9780804429573'],
  ])('%s → %s', (input, expected) => {
    expect(normalizeIsbn(input)).toBe(expected)
  })

  it.each(['9788535914848', '0306406153', '12345', ''])('rejects %s', (input) => {
    expect(normalizeIsbn(input)).toBeNull()
  })

  it('accepts only book barcodes (978/979 EAN-13) from the scanner', () => {
    expect(isBookBarcode('9788535914849')).toBe(true)
    expect(isBookBarcode('7891000315507')).toBe(false) // a valid EAN-13 for a grocery product
  })
})

describe('listing status badges (DESIGN.md)', () => {
  it('are filled, with the approved text color for each fill', () => {
    expect(statusBadgeClass).toEqual({
      Draft: 'bg-text-muted text-surface',
      Active: 'bg-primary text-surface',
      Reserved: 'bg-accent text-ink',
      Exchanged: 'bg-text-muted text-surface',
      Archived: 'bg-text-muted text-surface',
    })
  })
})
