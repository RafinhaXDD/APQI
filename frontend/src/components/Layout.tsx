import { Link, NavLink, Outlet, useNavigate } from 'react-router'
import { AqpiLogo } from './AqpiLogo'
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
        <div className="mx-auto flex max-w-5xl flex-wrap items-center justify-between gap-x-4 gap-y-1 px-4 py-3">
          <Link to="/" className="text-lg font-semibold">
            <AqpiLogo />
          </Link>
          <nav
            aria-label={t('nav.main')}
            className="order-last -mx-2 flex w-full items-center gap-1 overflow-x-auto text-sm whitespace-nowrap sm:order-none sm:mx-0 sm:w-auto sm:flex-1"
          >
            <NavLink to="/search" className={navLink}>
              {t('nav.search')}
            </NavLink>
            {status === 'authenticated' ? (
              <>
                <NavLink to="/listings/new" className={navLink}>
                  {t('nav.addBook')}
                </NavLink>
                <NavLink to="/listings/mine" className={navLink}>
                  {t('nav.myListings')}
                </NavLink>
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
