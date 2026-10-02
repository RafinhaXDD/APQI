import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useParams } from 'react-router'
import { Alert, Button, useErrorMessage } from '../../components/forms'
import { useI18n } from '../../i18n/context'
import { listingKeys, listingsApi } from '../../services/listings'
import {
  BeginnersTag,
  BookCover,
  CategoryTag,
  ConditionTag,
  StatusBadge,
  useDistanceText,
} from './components'
import { useAuth } from '../auth/context'
import { useSearchOrigin } from './origin'

export function ListingDetailsPage() {
  const { id = '' } = useParams()
  const { t } = useI18n()
  const errorMessage = useErrorMessage()
  const distance = useDistanceText()
  const { origin } = useSearchOrigin()
  const queryClient = useQueryClient()
  const { status } = useAuth()

  const listing = useQuery({
    queryKey: [...listingKeys.detail(id), origin],
    queryFn: ({ signal }) => listingsApi.get(id, origin, signal),
    // Wait for the session check: a signed-out fetch would hide owner controls and home-based distance.
    enabled: status !== 'loading',
  })
  const archive = useMutation({
    mutationFn: () => listingsApi.archive(id),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: listingKeys.all }),
  })

  if (listing.isPending) return <p className="text-text-muted">{t('common.loading')}</p>
  if (listing.isError) return <Alert kind="error">{errorMessage(listing.error)}</Alert>

  const { book, images } = listing.data
  const cover = images[0]?.displayUrl ?? book.coverUrl

  return (
    <article className="mx-auto max-w-3xl space-y-6">
      <div className="flex flex-col gap-6 sm:flex-row">
        <BookCover src={cover} title={book.title} className="h-72 w-full rounded-lg sm:w-48" />
        <div className="flex-1 space-y-2">
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={listing.data.status} />
            <CategoryTag category={listing.data.category} />
            <ConditionTag condition={listing.data.condition} />
            {listing.data.goodForBeginners && <BeginnersTag />}
          </div>
          <h1 className="text-2xl font-semibold">{book.title}</h1>
          {book.authors.length > 0 && <p className="text-lg">{book.authors.join(', ')}</p>}
          <p className="text-text-muted text-sm">
            {[
              book.isbn && t('listing.isbn', { isbn: book.isbn }),
              book.publishedYear && t('listing.published', { year: book.publishedYear }),
            ]
              .filter(Boolean)
              .join(' · ')}
          </p>
          <p>
            {listing.data.distance &&
              `${distance(listing.data.distance.km, listing.data.distance.isUpperBound)} · `}
            {listing.data.areaLabel}
          </p>
          <p className="text-text-muted text-sm">
            {t('listing.owner', { name: listing.data.owner.displayName })}
          </p>
        </div>
      </div>

      {listing.data.description && (
        <p className="border-border bg-surface rounded-lg border p-4 whitespace-pre-line">
          {listing.data.description}
        </p>
      )}

      {images.length > 1 && (
        <ul className="grid grid-cols-3 gap-2 sm:grid-cols-4">
          {images.map((image, index) => (
            <li key={image.id}>
              <a href={image.displayUrl} target="_blank" rel="noreferrer">
                <img
                  src={image.thumbnailUrl}
                  alt={t('listing.photo', { n: index + 1, total: images.length })}
                  loading="lazy"
                  className="aspect-square w-full rounded object-cover"
                />
              </a>
            </li>
          ))}
        </ul>
      )}

      {archive.isError && <Alert kind="error">{errorMessage(archive.error)}</Alert>}
      {listing.data.isMine ? (
        listing.data.status === 'Active' && (
          <div className="flex flex-wrap gap-3">
            <Link
              to={`/listings/${id}/edit`}
              className="font-display bg-primary text-surface hover:bg-primary-dark rounded-md px-4 py-2 font-semibold"
            >
              {t('listing.edit')}
            </Link>
            <Button
              type="button"
              variant="secondary"
              disabled={archive.isPending}
              onClick={() => window.confirm(t('listing.archiveConfirm')) && archive.mutate()}
            >
              {t('listing.archive')}
            </Button>
          </div>
        )
      ) : (
        <div className="space-y-2">
          <Button type="button" variant="secondary" disabled>
            {t('listing.request')}
          </Button>
          <p className="text-text-muted text-sm">{t('listing.requestSoon')}</p>
        </div>
      )}
    </article>
  )
}
