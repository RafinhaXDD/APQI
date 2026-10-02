import { Link } from 'react-router'
import { useI18n } from '../../i18n/context'
import type { MessageKey } from '../../i18n/pt-BR'
import { categories } from '../../services/listings'
import { useAuth } from '../auth/context'

const primaryLink =
  'font-display bg-primary text-surface hover:bg-primary-dark inline-block rounded-full px-6 py-3 font-bold transition-transform hover:-translate-y-0.5'
const secondaryLink =
  'font-display bg-accent text-ink inline-block rounded-full px-6 py-3 font-bold transition-transform hover:-translate-y-0.5'

const steps = [1, 2, 3, 4] as const

/** Staggered entrance (Tellet-style): each item rises a little after the previous one. */
const stagger = (index: number) => ({ animationDelay: `${index * 90}ms` })

export function HomePage() {
  const { t } = useI18n()
  const { status, user } = useAuth()

  return (
    <div className="space-y-16 pb-8">
      {/* Hero: light, warm, with a slowly drifting colour mesh (Stripe-style) behind the copy. */}
      <section className="bg-surface shadow-card relative overflow-hidden rounded-3xl">
        <div aria-hidden="true" className="pointer-events-none absolute inset-0">
          <div className="bg-accent/30 animate-drift absolute -top-24 -left-24 h-72 w-72 rounded-full blur-3xl" />
          <div className="bg-sage/25 animate-drift absolute -right-16 -bottom-24 h-80 w-80 rounded-full blur-3xl [animation-delay:-6s]" />
          <div className="bg-secondary/20 animate-drift absolute top-1/3 left-1/2 h-56 w-56 rounded-full blur-3xl [animation-delay:-12s]" />
        </div>
        <div className="relative grid items-center gap-6 p-6 sm:p-10 md:grid-cols-[1.2fr_1fr]">
          <div className="space-y-5">
            <p className="text-secondary-text animate-rise text-xs font-bold tracking-[0.12em] uppercase">
              {t('landing.eyebrow')}
            </p>
            <h1
              className="animate-rise text-4xl leading-tight font-black tracking-tight sm:text-5xl"
              style={stagger(1)}
            >
              {t('landing.title')}
            </h1>
            <p className="animate-rise max-w-prose text-lg leading-relaxed" style={stagger(2)}>
              {t('landing.lead')}
            </p>
            {status === 'authenticated' && user && (
              <p className="text-primary font-semibold">
                {t('home.welcome', { name: user.displayName })}
              </p>
            )}
            <div className="animate-rise flex flex-wrap gap-3 pt-2" style={stagger(3)}>
              <Link to="/search" className={primaryLink}>
                {t('landing.ctaSearch')}
              </Link>
              <Link
                to={status === 'authenticated' ? '/listings/new' : '/register'}
                className={secondaryLink}
              >
                {t('landing.ctaList')}
              </Link>
            </div>
          </div>
          <img
            src="/images/mascot-reading.webp"
            alt=""
            width={640}
            height={640}
            className="animate-rise mx-auto w-56 mix-blend-multiply sm:w-72"
            style={stagger(2)}
          />
        </div>
      </section>

      {/* How it works */}
      <section aria-labelledby="how" className="space-y-6">
        <div className="space-y-2">
          <p className="text-secondary-text text-xs font-bold tracking-[0.12em] uppercase">
            {t('landing.howEyebrow')}
          </p>
          <h2 id="how" className="text-3xl font-extrabold tracking-tight">
            {t('landing.howTitle')}
          </h2>
        </div>
        <ol className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {steps.map((step, index) => (
            <li
              key={step}
              className="bg-surface shadow-card animate-rise relative overflow-hidden rounded-2xl p-5"
              style={stagger(index)}
            >
              <span
                aria-hidden="true"
                className="text-primary/10 absolute top-2 left-4 text-6xl font-black"
              >
                0{step}
              </span>
              <img
                src={`/images/mascot-step${step}.webp`}
                alt=""
                width={640}
                height={640}
                loading="lazy"
                className="ml-auto h-28 w-28 object-contain mix-blend-multiply"
              />
              <h3 className="mt-2 text-lg font-bold">
                {t(`landing.step${step}.title` as MessageKey)}
              </h3>
              <p className="text-text mt-1 text-sm leading-relaxed">
                {t(`landing.step${step}.body` as MessageKey)}
              </p>
            </li>
          ))}
        </ol>
      </section>

      {/* Categories */}
      <section aria-labelledby="categories" className="space-y-6">
        <div className="space-y-2">
          <p className="text-sage-text text-xs font-bold tracking-[0.12em] uppercase">
            {t('landing.categoriesEyebrow')}
          </p>
          <h2 id="categories" className="text-3xl font-extrabold tracking-tight">
            {t('landing.categoriesTitle')}
          </h2>
        </div>
        <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3">
          {categories.map((category, index) => (
            <li key={category} className="animate-rise" style={stagger(index)}>
              <Link
                to={`/search?category=${category}`}
                className="bg-surface shadow-card text-primary block rounded-2xl p-5 font-bold transition-transform hover:-translate-y-0.5"
              >
                {t(`category.${category}`)}
              </Link>
            </li>
          ))}
        </ul>
        <div className="bg-sage text-ink flex flex-col items-start gap-3 rounded-2xl p-6 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h3 className="text-ink text-xl font-extrabold">{t('landing.beginnersTitle')}</h3>
            <p className="mt-1">{t('landing.beginnersBody')}</p>
          </div>
          <Link
            to="/search?beginners=true"
            className="font-display bg-primary-dark text-surface shrink-0 rounded-full px-5 py-2.5 font-bold"
          >
            {t('landing.beginnersCta')}
          </Link>
        </div>
      </section>

      {/* Manifesto + closing call to action (cool section, Raycast-style temperature change) */}
      <section className="bg-primary-dark text-surface space-y-6 rounded-3xl px-6 py-12 text-center sm:px-12">
        <p className="font-display text-surface text-3xl font-bold tracking-tight sm:text-4xl">
          {t('landing.manifesto')}
        </p>
        <p className="text-surface/80 mx-auto max-w-xl leading-relaxed">
          {t('landing.manifestoBody')}
        </p>
        <h2 className="text-surface pt-4 text-2xl font-extrabold">{t('landing.closingTitle')}</h2>
        <Link
          to={status === 'authenticated' ? '/listings/new' : '/register'}
          className={secondaryLink}
        >
          {t('landing.closingCta')}
        </Link>
      </section>
    </div>
  )
}

export function NotFoundPage() {
  const { t } = useI18n()
  return (
    <section className="bg-surface shadow-card mx-auto max-w-xl rounded-2xl p-8 text-center">
      <img
        src="/images/mascot-404.webp"
        alt=""
        width={640}
        height={640}
        className="mx-auto h-40 w-40 object-contain mix-blend-multiply"
      />
      <h1 className="mt-4 text-2xl font-extrabold">{t('notFound.title')}</h1>
      <p className="text-text-muted mt-2">{t('notFound.body')}</p>
      <Link
        to="/"
        className="text-primary mt-4 inline-block font-semibold underline underline-offset-2"
      >
        {t('common.backHome')}
      </Link>
    </section>
  )
}
