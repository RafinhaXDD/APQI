import { Link } from 'react-router'
import { useI18n } from '../../i18n/context'
import { useAuth } from '../auth/context'

const primaryLink =
  'bg-primary text-surface hover:bg-primary-dark inline-block rounded-md px-4 py-2 font-semibold'
const secondaryLink = 'bg-secondary text-text inline-block rounded-md px-4 py-2 font-semibold'

export function HomePage() {
  const { t } = useI18n()
  const { status, user } = useAuth()

  return (
    <section className="border-border bg-surface rounded-lg border p-6">
      <h1 className="text-2xl font-semibold">{t('home.title')}</h1>
      <p className="text-text-muted mt-2">{t('home.body')}</p>
      <div className="mt-6 flex flex-wrap gap-3">
        {status === 'authenticated' ? (
          <>
            {user && <p className="w-full">{t('home.welcome', { name: user.displayName })}</p>}
            <Link to="/profile" className={primaryLink}>
              {t('home.ctaProfile')}
            </Link>
          </>
        ) : (
          <>
            <Link to="/register" className={primaryLink}>
              {t('home.ctaRegister')}
            </Link>
            <Link to="/login" className={secondaryLink}>
              {t('home.ctaLogin')}
            </Link>
          </>
        )}
      </div>
    </section>
  )
}

export function NotFoundPage() {
  const { t } = useI18n()
  return (
    <section className="border-border bg-surface rounded-lg border p-6">
      <h1 className="text-2xl font-semibold">{t('notFound.title')}</h1>
      <p className="text-text-muted mt-2">{t('notFound.body')}</p>
      <Link to="/" className="text-primary mt-4 inline-block underline underline-offset-2">
        {t('common.backHome')}
      </Link>
    </section>
  )
}
