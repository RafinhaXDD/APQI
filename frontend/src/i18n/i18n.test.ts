import { describe, expect, it } from 'vitest'
import { detectLanguage, dictionaries, format, translate, translatePlural } from './i18n'

describe('detectLanguage (CLAUDE.md: browser first, pt-BR fallback)', () => {
  it.each([
    [['pt-BR'], 'pt-BR'],
    [['pt-PT', 'en'], 'pt-BR'],
    [['en-US'], 'en'],
    [['en-GB', 'pt-BR'], 'en'],
    [['fr-FR', 'en'], 'en'],
    [['fr-FR', 'de'], 'pt-BR'],
    [[], 'pt-BR'],
  ])('%j → %s', (browser, expected) => {
    expect(detectLanguage(browser, null)).toBe(expected)
  })

  it('a remembered choice wins over the browser', () => {
    expect(detectLanguage(['pt-BR'], 'en')).toBe('en')
  })

  it('ignores a remembered value that is not a supported language', () => {
    expect(detectLanguage(['en-US'], 'klingon')).toBe('en')
  })
})

describe('translations', () => {
  it('both dictionaries have exactly the same keys and no empty strings', () => {
    const pt = Object.keys(dictionaries['pt-BR']).sort()
    const en = Object.keys(dictionaries.en).sort()

    expect(en).toEqual(pt)
    for (const dictionary of Object.values(dictionaries)) {
      expect(Object.values(dictionary).filter((text) => text.trim() === '')).toEqual([])
    }
  })

  it('has a message for every error code the API returns', () => {
    // Mirrors BookExchange.Application.Shared.ErrorCodes.
    const serverCodes = [
      'validation.failed',
      'auth.unauthorized',
      'auth.invalid_credentials',
      'auth.email_not_confirmed',
      'auth.session_expired',
      'auth.invalid_link',
      'auth.wrong_password',
      'rate_limited',
      'conflict',
      'not_found',
      'server_error',
    ]
    for (const language of ['pt-BR', 'en'] as const) {
      for (const code of [...serverCodes, 'network_error']) {
        expect(dictionaries[language], `${language}: errors.${code}`).toHaveProperty([
          `errors.${code}`,
        ])
      }
    }
  })

  it('fills placeholders and leaves unknown ones visible', () => {
    expect(format('Olá, {name}! {missing}', { name: 'Ana' })).toBe('Olá, Ana! {missing}')
    expect(translate('en', 'home.welcome', { name: 'Ben' })).toBe('Hi, Ben!')
  })

  it('picks the plural form per language', () => {
    expect(translatePlural('pt-BR', 'profile.credits', 1)).toBe('1 ficha disponível')
    expect(translatePlural('pt-BR', 'profile.credits', 0)).toBe('0 fichas disponíveis')
    expect(translatePlural('en', 'profile.credits', 1)).toBe('1 token available')
    expect(translatePlural('en', 'profile.credits', 3)).toBe('3 tokens available')
  })
})
