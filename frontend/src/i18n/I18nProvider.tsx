import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { detectLanguage, type Language } from './i18n'
import { I18nContext } from './context'

const storageKey = 'aqpi.language'

function readStored(): string | null {
  try {
    return localStorage.getItem(storageKey)
  } catch {
    return null
  }
}

function writeStored(language: Language) {
  try {
    localStorage.setItem(storageKey, language)
  } catch {
    // Private mode or blocked storage: the choice just won't be remembered.
  }
}

export function I18nProvider({
  children,
  initialLanguage,
}: {
  children: ReactNode
  initialLanguage?: Language
}) {
  const [language, setLanguageState] = useState<Language>(
    () =>
      initialLanguage ?? detectLanguage(navigator.languages ?? [navigator.language], readStored()),
  )

  useEffect(() => {
    document.documentElement.lang = language
  }, [language])

  const setLanguage = useCallback((next: Language) => {
    writeStored(next)
    setLanguageState(next)
  }, [])

  const value = useMemo(() => ({ language, setLanguage }), [language, setLanguage])
  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>
}
