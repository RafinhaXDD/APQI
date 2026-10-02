import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { onSessionExpired, refreshSession } from '../../services/api'
import { authApi, queryKeys } from '../../services/endpoints'
import { AuthContext, type AuthStatus } from './context'

/**
 * Session state for the whole app. On load it tries to restore the session from the refresh cookie;
 * the access token itself stays inside the API client's memory.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [status, setStatus] = useState<AuthStatus>('loading')

  useEffect(() => {
    let active = true
    void refreshSession().then((ok) => {
      if (active) setStatus(ok ? 'authenticated' : 'anonymous')
    })
    return () => {
      active = false
    }
  }, [])

  useEffect(
    () =>
      onSessionExpired(() => {
        setStatus('anonymous')
        queryClient.removeQueries()
      }),
    [queryClient],
  )

  const me = useQuery({
    queryKey: queryKeys.me,
    queryFn: ({ signal }) => authApi.me(signal),
    enabled: status === 'authenticated',
    staleTime: 60_000,
  })

  const login = useCallback(
    async (email: string, password: string) => {
      await authApi.login({ email, password })
      queryClient.removeQueries()
      setStatus('authenticated')
    },
    [queryClient],
  )

  const logout = useCallback(async () => {
    try {
      await authApi.logout()
    } finally {
      queryClient.removeQueries()
      setStatus('anonymous')
    }
  }, [queryClient])

  const value = useMemo(
    () => ({ status, user: me.data ?? null, login, logout }),
    [status, me.data, login, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
