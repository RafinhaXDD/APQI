import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router'
import { useI18n } from '../../i18n/context'
import { useAuth } from './context'

/** Client-side guard for signed-in pages. The API enforces access on its own (R-7). */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { status } = useAuth()
  const { t } = useI18n()
  const location = useLocation()

  if (status === 'loading') return <p className="text-text-muted p-6">{t('common.loading')}</p>
  if (status === 'anonymous')
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  return children
}
