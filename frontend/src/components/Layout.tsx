import { Link, NavLink, Outlet, useNavigate } from 'react-router'
import { useAuth } from '../features/auth/context'
import { useI18n } from '../i18n/context'
import { isLanguage, languages } from '../i18n/i18n'

const navLink = ({ isActive }: { isActive: boolean }) =>
  `rounded px-2 py-1 ${isActive ? 'underline underline-offset-4' : 'hover:underline'}`

export function Layout() {
  const { t, language, setLanguage } = useI18n()
  const { status, logout } = useAuth()
  const navigate = useNavigate()

  return (
    <div className="flex min-h-dvh flex-col">
      <header className="bg-primary-dark text-surface">
        <div className="mx-auto flex max-w-5xl flex-wrap items-center gap-x-4 gap-y-2 px-4 py-3">
          <Link to="/" className="text-lg font-semibold">
            {t('app.name')}
          </Link>
          <nav
            aria-label={t('nav.main')}
            className="flex flex-1 flex-wrap items-center gap-2 text-sm"
          >
            {status === 'authenticated' ? (
              <>
                <NavLink to="/profile" className={navLink}>
                  {t('nav.profile')}
                </NavLink>
                <button
                  type="button"
                  className="rounded px-2 py-1 hover:underline"
                  onClick={() => void logout().then(() => navigate('/'))}
                >
                  {t('nav.logout')}
                </button>
              </>
            ) : (
              status === 'anonymous' && (
                <>
                  <NavLink to="/login" className={navLink}>
                    {t('nav.login')}
                  </NavLink>
                  <NavLink to="/register" className={navLink}>
                    {t('nav.register')}
                  </NavLink>
                </>
              )
            )}
          </nav>
          <label className="flex items-center gap-2 text-sm">
            <span className="sr-only">{t('nav.language')}</span>
            <select
              value={language}
              onChange={(event) =>
                isLanguage(event.target.value) && setLanguage(event.target.value)
              }
              className="bg-primary-dark text-surface border-surface rounded border px-2 py-1"
            >
              {languages.map((option) => (
                <option key={option} value={option}>
                  {t(`lang.${option}`)}
                </option>
              ))}
            </select>
          </label>
        </div>
      </header>

      <main className="mx-auto w-full max-w-5xl flex-1 px-4 py-8">
        <Outlet />
      </main>

      <footer className="bg-primary-dark text-surface">
        <div className="mx-auto max-w-5xl px-4 py-3 text-sm">{t('app.tagline')}</div>
      </footer>
    </div>
  )
}
