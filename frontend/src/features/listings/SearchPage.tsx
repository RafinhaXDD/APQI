import { useInfiniteQuery, useQuery } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router'
import { Alert, Button, useErrorMessage } from '../../components/forms'
import { useI18n } from '../../i18n/context'
import { profileApi, queryKeys } from '../../services/endpoints'
import {
  categories,
  conditions,
  listingKeys,
  listingLimits,
  listingsApi,
  sorts,
  type Category,
  type Condition,
  type SearchParams,
  type Sort,
} from '../../services/listings'
import { useAuth } from '../auth/context'
import { EmptyState, ListingCard } from './components'
import { getDevicePosition, useSearchOrigin } from './origin'

function useDebounced<T>(value: T, delayMs: number) {
  const [debounced, setDebounced] = useState(value)
  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delayMs)
    return () => clearTimeout(timer)
  }, [value, delayMs])
  return debounced
}

const selectClass = 'border-border bg-surface w-full rounded-md border px-3 py-2'

export function SearchPage() {
  const { t, tPlural } = useI18n()
  const errorMessage = useErrorMessage()
  const { status } = useAuth()
  const { origin, setOrigin } = useSearchOrigin()
  const [locating, setLocating] = useState(false)
  const [locationFailed, setLocationFailed] = useState(false)
  const [q, setQ] = useState('')
  const [radiusKm, setRadiusKm] = useState<number>(listingLimits.defaultRadiusKm)
  const [condition, setCondition] = useState<Condition | ''>('')
  // Landing-page links open the search pre-filtered, e.g. /search?category=Philosophy or ?beginners=true.
  const [urlParams] = useSearchParams()
  const [category, setCategory] = useState<Category | ''>(() => {
    const value = urlParams.get('category')
    return (categories as readonly string[]).includes(value ?? '') ? (value as Category) : ''
  })
  const [beginners, setBeginners] = useState(urlParams.get('beginners') === 'true')
  const [sort, setSort] = useState<Sort>('Distance')
  const debouncedQ = useDebounced(q, 300)

  const profile = useQuery({
    queryKey: queryKeys.profile,
    queryFn: ({ signal }) => profileApi.get(signal),
    enabled: status === 'authenticated',
  })
  const homeArea = profile.data?.homeArea ?? null
  // Device location wins; otherwise a signed-in user's home area is used by the server.
  const canSearch = origin !== null || homeArea !== null
  // A disabled query (signed out) stays "pending" forever, so check the auth status first.
  const locationKnown = status === 'anonymous' || profile.isSuccess || profile.isError
  const params: SearchParams = {
    origin,
    q: debouncedQ,
    radiusKm,
    condition,
    category,
    beginners,
    sort,
  }

  const results = useInfiniteQuery({
    queryKey: listingKeys.search(params),
    queryFn: ({ pageParam, signal }) => listingsApi.search(params, pageParam, signal),
    initialPageParam: 1,
    getNextPageParam: (last) =>
      last.page * last.pageSize < last.totalCount ? last.page + 1 : undefined,
    enabled: canSearch && status !== 'loading',
  })

  const requestMyLocation = async () => {
    setLocating(true)
    setLocationFailed(false)
    try {
      setOrigin(await getDevicePosition())
    } catch {
      setLocationFailed(true)
    } finally {
      setLocating(false)
    }
  }

  const items = results.data?.pages.flatMap((page) => page.items) ?? []
  const total = results.data?.pages[0]?.totalCount ?? 0

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">{t('search.title')}</h1>

      <div className="border-border bg-surface space-y-4 rounded-lg border p-4">
        <div className="flex flex-wrap items-center gap-3 text-sm">
          {origin ? (
            <span>{t('search.usingMyLocation')}</span>
          ) : (
            homeArea && <span>{t('search.usingHome', { area: homeArea.label })}</span>
          )}
          <Button
            type="button"
            variant="link"
            onClick={() => void requestMyLocation()}
            disabled={locating}
          >
            {locating ? t('profile.locating') : t('search.useMyLocation')}
          </Button>
        </div>
        {locationFailed && <Alert kind="info">{t('profile.locationDenied')}</Alert>}

        <div>
          <label htmlFor="search-q" className="mb-1 block text-sm font-medium">
            {t('search.query')}
          </label>
          <input
            id="search-q"
            type="search"
            value={q}
            onChange={(event) => setQ(event.target.value)}
            className="border-border bg-surface w-full rounded-md border px-3 py-2"
          />
        </div>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 sm:items-end">
          {' '}
          <div>
            {' '}
            <label htmlFor="search-category" className="mb-1 block text-sm font-medium">
              {' '}
              {t('search.category')}{' '}
            </label>{' '}
            <select
              id="search-category"
              value={category}
              onChange={(event) => setCategory(event.target.value as Category | '')}
              className={selectClass}
            >
              {' '}
              <option value="">{t('search.anyCategory')}</option>{' '}
              {categories.map((value) => (
                <option key={value} value={value}>
                  {' '}
                  {t(`category.${value}`)}{' '}
                </option>
              ))}{' '}
            </select>{' '}
          </div>{' '}
          <label className="flex items-center gap-2 py-2 text-sm">
            {' '}
            <input
              type="checkbox"
              checked={beginners}
              onChange={(event) => setBeginners(event.target.checked)}
              className="accent-primary h-4 w-4"
            />{' '}
            {t('search.beginnersOnly')}{' '}
          </label>{' '}
        </div>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
          <div>
            <label htmlFor="search-radius" className="mb-1 block text-sm font-medium">
              {t('search.radius')}
            </label>
            <select
              id="search-radius"
              value={radiusKm}
              onChange={(event) => setRadiusKm(Number(event.target.value))}
              className={selectClass}
            >
              {listingLimits.radiusOptions.map((km) => (
                <option key={km} value={km}>
                  {t('search.radiusOption', { km })}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label htmlFor="search-condition" className="mb-1 block text-sm font-medium">
              {t('search.condition')}
            </label>
            <select
              id="search-condition"
              value={condition}
              onChange={(event) => setCondition(event.target.value as Condition | '')}
              className={selectClass}
            >
              <option value="">{t('search.anyCondition')}</option>
              {conditions.map((value) => (
                <option key={value} value={value}>
                  {t(`condition.${value}`)}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label htmlFor="search-sort" className="mb-1 block text-sm font-medium">
              {t('search.sort')}
            </label>
            <select
              id="search-sort"
              value={sort}
              onChange={(event) => setSort(event.target.value as Sort)}
              className={selectClass}
            >
              {sorts.map((value) => (
                <option key={value} value={value}>
                  {t(`search.sort.${value}`)}
                </option>
              ))}
            </select>
          </div>
        </div>
      </div>

      {!canSearch && locationKnown && <Alert kind="info">{t('search.needLocation')}</Alert>}
      {results.isError && <Alert kind="error">{errorMessage(results.error)}</Alert>}
      {canSearch && results.isPending && <p className="text-text-muted">{t('common.loading')}</p>}

      {results.isSuccess && (
        <section aria-live="polite" className="space-y-3">
          <p className="text-text-muted text-sm">{tPlural('search.results', total)}</p>
          {items.length === 0 ? (
            <EmptyState image="/images/mascot-step3.webp">{t('search.empty')}</EmptyState>
          ) : (
            <ul className="grid grid-cols-1 gap-3 md:grid-cols-2">
              {items.map((listing) => (
                <li key={listing.id}>
                  <ListingCard listing={listing} />
                </li>
              ))}
            </ul>
          )}
          {results.hasNextPage && (
            <Button
              type="button"
              variant="secondary"
              onClick={() => void results.fetchNextPage()}
              disabled={results.isFetchingNextPage}
            >
              {results.isFetchingNextPage ? t('common.loading') : t('search.loadMore')}
            </Button>
          )}
        </section>
      )}
    </div>
  )
}
