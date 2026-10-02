// @vitest-environment node
import { describe, expect, it } from 'vitest'
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { contrastRatio, parseColorTokens } from './contrast'

const css = readFileSync(new URL('../index.css', import.meta.url), 'utf8')
const tokens = parseColorTokens(css)

// Duda's AQPI brand (CLAUDE.md, Phase 5b); changing a token must be a deliberate decision.
const expected: Record<string, string> = {
  primary: '#1d3d5c',
  'primary-dark': '#0f1f2e',
  secondary: '#d4936d',
  accent: '#d0b088',
  sage: '#8b9d83',
  background: '#f5f3ef',
  surface: '#ffffff',
  text: '#4a4a4a',
  ink: '#0f1f2e',
  'text-muted': '#6a6a6a',
  'secondary-text': '#9e5a36',
  'sage-text': '#5e6f57',
  border: '#e2dfd6',
  success: '#3e7c59',
  error: '#b94a48',
}

const AA_TEXT = 4.5
const AA_NON_TEXT = 3

/** [foreground, background, minimum ratio, where it is used] */
const pairs: [string, string, number, string][] = [
  ['text', 'background', AA_TEXT, 'body text on page'],
  ['text', 'surface', AA_TEXT, 'body text on cards/inputs'],
  ['text-muted', 'background', AA_TEXT, 'metadata on page'],
  ['text-muted', 'surface', AA_TEXT, 'metadata on cards'],
  ['primary', 'background', AA_TEXT, 'headings and links on page'],
  ['primary', 'surface', AA_TEXT, 'headings and links on cards'],
  ['secondary-text', 'background', AA_TEXT, 'terracotta labels'],
  ['sage-text', 'background', AA_TEXT, 'sage labels'],
  ['error', 'surface', AA_TEXT, 'error messages'],
  ['error', 'background', AA_TEXT, 'error messages on page'],
  ['surface', 'primary', AA_TEXT, 'primary buttons, Active badge'],
  ['surface', 'primary-dark', AA_TEXT, 'header/footer'],
  ['ink', 'secondary', AA_TEXT, 'secondary CTA, Disputed badge'],
  ['ink', 'accent', AA_TEXT, 'Pending/Reserved badge, ficha counter'],
  ['ink', 'sage', AA_TEXT, 'sage chips'],
  ['surface', 'success', AA_TEXT, 'Accepted/Completed badge'],
  ['surface', 'error', AA_TEXT, 'Rejected badge, destructive button'],
  ['surface', 'text-muted', AA_TEXT, 'Cancelled/Expired/Exchanged badge'],
  ['primary', 'surface', AA_NON_TEXT, 'focus ring'],
]

describe('design tokens', () => {
  it('defines exactly the brand tokens with the brand values', () => {
    expect(tokens).toEqual(expected)
  })

  it.each(pairs)('%s on %s meets %d:1 (%s)', (foreground, background, minimum) => {
    expect(contrastRatio(tokens[foreground], tokens[background])).toBeGreaterThanOrEqual(minimum)
  })

  it('documents why terracotta, gold and sage are never text colors', () => {
    for (const color of ['secondary', 'accent', 'sage']) {
      expect(contrastRatio(tokens[color], tokens.background)).toBeLessThan(AA_TEXT)
    }
    // ...and why plain charcoal text isn't used on those fills either.
    expect(contrastRatio(tokens.text, tokens.secondary)).toBeLessThan(AA_TEXT)
    expect(contrastRatio(tokens.text, tokens.accent)).toBeLessThan(AA_TEXT)
  })

  it('no component uses a brand accent as text, or unreadable text on an accent fill', () => {
    const offenders: string[] = []
    for (const file of sourceFiles(fileURLToPath(new URL('..', import.meta.url)))) {
      const source = readFileSync(file, 'utf8')
      const accentAsText = /\btext-(secondary|accent|sage)(?![-\w])/.test(source)
      const badFill = /\bbg-(secondary|accent|sage)\b[^'"`]*\btext-(text|surface)\b/.test(source)
      if (accentAsText || badFill) offenders.push(file)
    }
    expect(offenders).toEqual([])
  })
})

describe('contrastRatio', () => {
  it('matches known WCAG reference values', () => {
    expect(contrastRatio('#000000', '#ffffff')).toBeCloseTo(21, 5)
    expect(contrastRatio('#ffffff', '#ffffff')).toBeCloseTo(1, 5)
    expect(contrastRatio('#767676', '#ffffff')).toBeCloseTo(4.54, 2)
  })
})

function sourceFiles(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name)
    if (statSync(path).isDirectory()) return sourceFiles(path)
    return /\.tsx$/.test(name) && !/\.test\.tsx$/.test(name) ? [path] : []
  })
}
