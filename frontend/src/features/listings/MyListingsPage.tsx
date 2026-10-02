import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router'
import { Alert, useErrorMessage } from '../../components/forms'
import { useI18n } from '../../i18n/context'
import { listingKeys, listingsApi } from '../../services/listings'
import { BookCover, CategoryTag, ConditionTag, EmptyState, StatusBadge } from './components'

export function MyListingsPage() {
  const { t, tPlural } = useI18n()
  const errorMessage = useErrorMessage()
  const mine = useQuery({
    queryKey: listingKeys.mine,
    queryFn: ({ signal }) => listingsApi.mine(signal),
  })

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold">{t('mine.title')}</h1>
        <Link
          to="/listings/new"
          className="font-display bg-primary text-surface hover:bg-primary-dark rounded-md px-4 py-2 font-semibold"
        >
          {t('nav.addBook')}
        </Link>
      </div>

      {mine.isPending && <p className="text-text-muted">{t('common.loading')}</p>}
      {mine.isError && <Alert kind="error">{errorMessage(mine.error)}</Alert>}
      {mine.isSuccess && mine.data.length === 0 && (
        <EmptyState image="/images/mascot-step1.webp">{t('mine.empty')}</EmptyState>
      )}
      {mine.isSuccess && mine.data.length > 0 && (
        <ul className="space-y-3">
          {mine.data.map((item) => (
            <li key={item.id}>
              <Link
                to={`/listings/${item.id}`}
                className="border-border bg-surface flex gap-4 rounded-lg border p-3 hover:shadow-sm"
              >
                <BookCover
                  src={item.thumbnailUrl ?? item.coverUrl}
                  title={item.title}
                  className="h-24 w-16 shrink-0 rounded"
                />
                <div className="min-w-0 flex-1 space-y-1">
                  <h2 className="truncate font-semibold">{item.title}</h2>
                  <p className="text-text-muted truncate text-sm">{item.authors.join(', ')}</p>
                  <div className="flex flex-wrap items-center gap-2">
                    <StatusBadge status={item.status} />
                    <CategoryTag category={item.category} />
                    <ConditionTag condition={item.condition} />
                    <span className="text-text-muted text-sm">
                      {tPlural('mine.photos', item.imageCount)} · {item.areaLabel}
                    </span>
                  </div>
                </div>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
