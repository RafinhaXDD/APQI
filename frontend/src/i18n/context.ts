import { createContext, useCallback, useContext } from 'react'
import { translate, translatePlural, type Language, type Params, type PluralBase } from './i18n'
import type { MessageKey } from './pt-BR'

export const I18nContext = createContext<{
  language: Language
  setLanguage: (language: Language) => void
} | null>(null)

export function useI18n() {
  const context = useContext(I18nContext)
  if (!context) throw new Error('useI18n must be used inside <I18nProvider>.')
  const { language, setLanguage } = context

  const t = useCallback(
    (key: MessageKey, params?: Params) => translate(language, key, params),
    [language],
  )
  const tPlural = useCallback(
    (base: PluralBase, count: number, params?: Params) =>
      translatePlural(language, base, count, params),
    [language],
  )

  return { language, setLanguage, t, tPlural }
}
