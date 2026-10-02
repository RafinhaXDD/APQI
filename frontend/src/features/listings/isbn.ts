/** Mirror of BookExchange.Domain.Books.Isbn: returns the ISBN-13 for a valid ISBN-10/13, or null. */
export function normalizeIsbn(input: string): string | null {
  const compact = input.replace(/[\s-]/g, '').toUpperCase()
  if (/^\d{13}$/.test(compact))
    return checksum13(compact.slice(0, 12)) === compact[12] ? compact : null
  if (/^\d{9}[\dX]$/.test(compact) && isValid10(compact)) {
    const body = '978' + compact.slice(0, 9)
    return body + checksum13(body)
  }
  return null
}

/** Book barcodes are EAN-13 starting with 978 or 979 ("Bookland"); other EANs aren't ISBNs. */
export function isBookBarcode(text: string) {
  return /^97[89]\d{10}$/.test(text) && normalizeIsbn(text) !== null
}

function checksum13(first12: string) {
  const sum = [...first12].reduce(
    (total, digit, i) => total + Number(digit) * (i % 2 === 0 ? 1 : 3),
    0,
  )
  return String((10 - (sum % 10)) % 10)
}

function isValid10(chars: string) {
  const sum = [...chars].reduce(
    (total, char, i) => total + (char === 'X' ? 10 : Number(char)) * (10 - i),
    0,
  )
  return sum % 11 === 0
}
