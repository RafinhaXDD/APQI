// @vitest-environment node
import { describe, expect, it } from 'vitest'
import { readFileSync } from 'node:fs'
import { contrastRatio, parseColorTokens } from './contrast'

const css = readFileSync(new URL('../index.css', import.meta.url), 'utf8')
const tokens = parseColorTokens(css)

// SPEC §11.4 values; changing a token must be a deliberate decision.
const expected: Record<string, string> = {
  primary: '#2f5d50',
  'primary-dark': '#1f4037',
  secondary: '#c9785d',
  accent: '#d9a441',
  background: '#f7f4ed',
  surface: '#fffdf8',
  text: '#252525',
  'text-muted': '#706d67',
  border: '#ded9cf',
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
  ['primary', 'background', AA_TEXT, 'links on page'],
  ['primary', 'surface', AA_TEXT, 'links on cards'],
  ['primary-dark', 'surface', AA_TEXT, 'emphasis text'],
  ['error', 'surface', AA_TEXT, 'error messages'],
  ['surface', 'primary', AA_TEXT, 'primary buttons, Active badge'],
  ['surface', 'primary-dark', AA_TEXT, 'header/footer'],
  // SPEC says surface on secondary, but that is 3.26:1 (fails AA); CLAUDE.md design fix: text on secondary.
  ['text', 'secondary', AA_TEXT, 'secondary CTA, Disputed badge'],
  ['surface', 'success', AA_TEXT, 'Accepted/Completed badge'],
  ['surface', 'error', AA_TEXT, 'Rejected badge, destructive button'],
  ['surface', 'text-muted', AA_TEXT, 'Cancelled/Expired/Exchanged badge'],
  ['text', 'accent', AA_TEXT, 'Pending/Reserved badge, unread badge'],
  ['primary', 'surface', AA_NON_TEXT, 'focus ring'],
]

describe('design tokens', () => {
  it('defines exactly the SPEC tokens with the SPEC values', () => {
    expect(tokens).toEqual(expected)
  })

  it.each(pairs)('%s on %s meets %d:1 (%s)', (foreground, background, minimum) => {
    expect(contrastRatio(tokens[foreground], tokens[background])).toBeGreaterThanOrEqual(minimum)
  })
})

describe('contrastRatio', () => {
  it('matches known WCAG reference values', () => {
    expect(contrastRatio('#000000', '#ffffff')).toBeCloseTo(21, 5)
    expect(contrastRatio('#ffffff', '#ffffff')).toBeCloseTo(1, 5)
    expect(contrastRatio('#767676', '#ffffff')).toBeCloseTo(4.54, 2)
  })
})
