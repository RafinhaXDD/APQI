import { en } from './en'
import { ptBR, type MessageKey, type Messages } from './pt-BR'

export const languages = ['pt-BR', 'en'] as const
export type Language = (typeof languages)[number]

/** CLAUDE.md: anything that isn't Portuguese or English falls back to pt-BR. */
export const defaultLanguage: Language = 'pt-BR'

export const dictionaries: Record<Language, Messages> = { 'pt-BR': ptBR, en }

export function isLanguage(value: unknown): value is Language {
  return typeof value === 'string' && (languages as readonly string[]).includes(value)
}

/**
 * Picks the UI language: a remembered choice wins; otherwise the first browser language that is
 * Portuguese (any region) or English (any region); otherwise pt-BR.
 */
export function detectLanguage(
  browserLanguages: readonly string[],
  stored: string | null,
): Language {
  if (isLanguage(stored)) return stored
  for (const tag of browserLanguages) {
    const base = tag.toLowerCase().split('-')[0]
    if (base === 'pt') return 'pt-BR'
    if (base === 'en') return 'en'
  }
  return defaultLanguage
}

export type Params = Record<string, string | number>

export function format(template: string, params?: Params): string {
  if (!params) return template
  return template.replace(/\{(\w+)\}/g, (match, name: string) =>
    name in params ? String(params[name]) : match,
  )
}

export type PluralBase = {
  [K in MessageKey]: K extends `${infer Base}.one` ? Base : never
}[MessageKey]

export function translate(language: Language, key: MessageKey, params?: Params): string {
  return format(dictionaries[language][key], params)
}

export function translatePlural(
  language: Language,
  base: PluralBase,
  count: number,
  params?: Params,
): string {
  // Both languages use the singular only for exactly 1 (CLDR's pt rule would also make 0 singular).
  const category = count === 1 ? 'one' : 'other'
  return translate(language, `${base}.${category}` as MessageKey, { count, ...params })
}
