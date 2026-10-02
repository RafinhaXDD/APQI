import { createContext, useContext, useMemo, useState, type ReactNode } from 'react'
import type { Origin } from '../../services/listings'

/**
 * The searcher's device location, kept in memory only (never in the URL or storage) and shared between
 * the search page and listing details so both show distances from the same point.
 */
const SearchOriginContext = createContext<{
  origin: Origin | null
  setOrigin: (origin: Origin | null) => void
} | null>(null)

export function SearchOriginProvider({ children }: { children: ReactNode }) {
  const [origin, setOrigin] = useState<Origin | null>(null)
  const value = useMemo(() => ({ origin, setOrigin }), [origin])
  return <SearchOriginContext.Provider value={value}>{children}</SearchOriginContext.Provider>
}

export function useSearchOrigin() {
  const context = useContext(SearchOriginContext)
  if (!context) throw new Error('useSearchOrigin must be used inside <SearchOriginProvider>.')
  return context
}

/** Asks the browser for the current position, coarse accuracy is enough. */
export function getDevicePosition(): Promise<Origin> {
  return new Promise((resolve, reject) => {
    if (!('geolocation' in navigator)) {
      reject(new Error('Geolocation unavailable'))
      return
    }
    navigator.geolocation.getCurrentPosition(
      ({ coords }) => resolve({ lat: coords.latitude, lng: coords.longitude }),
      reject,
      { enableHighAccuracy: false, timeout: 10_000, maximumAge: 300_000 },
    )
  })
}
