import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { useI18n } from '../../i18n/context'
import type { Category, Condition, ListingStatus, ListingSummary } from '../../services/listings'

/** DESIGN.md: always a filled badge with a label; text color chosen for WCAG AA on each fill. */
export const statusBadgeClass: Record<ListingStatus, string> = {
  Draft: 'bg-text-muted text-surface',
  Active: 'bg-primary text-surface',
  Reserved: 'bg-accent text-ink',
  Exchanged: 'bg-text-muted text-surface',
  Archived: 'bg-text-muted text-surface',
}

export function StatusBadge({ status }: { status: ListingStatus }) {
  const { t } = useI18n()
  return (
    <span
      className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${statusBadgeClass[status]}`}
    >
      {t(`status.${status}`)}
    </span>
  )
}

export function ConditionTag({ condition }: { condition: Condition }) {
  const { t } = useI18n()
  return (
    <span className="border-border text-text rounded-full border px-2.5 py-0.5 text-xs">
      {t(`condition.${condition}`)}
    </span>
  )
}

export function CategoryTag({ category }: { category: Category }) {
  const { t } = useI18n()
  return (
    <span className="bg-background text-primary rounded-full px-2.5 py-0.5 text-xs font-semibold">
      {t(`category.${category}`)}
    </span>
  )
}

/** "Bom para começar": sage fill with ink text (DESIGN.md readable pair). */
export function BeginnersTag() {
  const { t } = useI18n()
  return (
    <span className="bg-sage text-ink rounded-full px-2.5 py-0.5 text-xs font-semibold">
      {t('category.beginners')}
    </span>
  )
}

export function useDistanceText() {
  const { t, language } = useI18n()
  const format = new Intl.NumberFormat(language, { maximumFractionDigits: 1 })
  return (km: number, isUpperBound: boolean) =>
    isUpperBound ? t('distance.lessThanOne') : t('distance.km', { km: format.format(km) })
}

/** Uploaded photo first, then the Open Library cover, then a neutral placeholder. */
export function BookCover({
  src,
  title,
  className = '',
}: {
  src: string | null
  title: string
  className?: string
}) {
  if (!src) {
    return (
      <div
        aria-hidden="true"
        className={`bg-background text-text-muted flex items-center justify-center text-3xl font-semibold ${className}`}
      >
        {title.slice(0, 1).toUpperCase()}
      </div>
    )
  }
  return <img src={src} alt="" loading="lazy" className={`object-cover ${className}`} />
}

export function ListingCard({ listing }: { listing: ListingSummary }) {
  const { t } = useI18n()
  const distance = useDistanceText()
  return (
    <Link
      to={`/listings/${listing.id}`}
      className="border-border bg-surface flex gap-4 rounded-lg border p-3 hover:shadow-sm"
    >
      <BookCover
        src={listing.thumbnailUrl ?? listing.coverUrl}
        title={listing.title}
        className="h-28 w-20 shrink-0 rounded"
      />
      <div className="min-w-0 flex-1 space-y-1">
        <h3 className="truncate font-semibold">{listing.title}</h3>
        {listing.authors.length > 0 && (
          <p className="text-text-muted truncate text-sm">
            {t('search.by', { authors: listing.authors.join(', ') })}
          </p>
        )}
        <div className="flex flex-wrap items-center gap-2 pt-1">
          <CategoryTag category={listing.category} />
          <ConditionTag condition={listing.condition} />
          {listing.goodForBeginners && <BeginnersTag />}
          {listing.isMine && (
            <span className="bg-accent text-ink rounded-full px-2.5 py-0.5 text-xs font-semibold">
              {t('search.yours')}
            </span>
          )}
        </div>
        <p className="text-sm">
          {distance(listing.distanceKm, listing.distanceIsUpperBound)} · {listing.areaLabel}
        </p>
      </div>
    </Link>
  )
}

/** Friendly empty state with the AQPI mascot (decorative image). */
export function EmptyState({ image, children }: { image: string; children: ReactNode }) {
  return (
    <div className="bg-surface shadow-card flex flex-col items-center gap-3 rounded-2xl p-6 text-center">
      <img
        src={image}
        alt=""
        width={640}
        height={640}
        loading="lazy"
        className="h-32 w-32 object-contain mix-blend-multiply"
      />
      <div>{children}</div>
    </div>
  )
}
