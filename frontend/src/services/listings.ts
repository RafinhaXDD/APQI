import { apiRequest } from './api'

export const conditions = ['New', 'LikeNew', 'Good', 'Fair', 'Worn'] as const
export type Condition = (typeof conditions)[number]
export const categories = [
  'Fiction',
  'NonFiction',
  'PersonalDevelopment',
  'Philosophy',
  'CareerStrategy',
  'Classics',
] as const
export type Category = (typeof categories)[number]
export type ListingStatus = 'Draft' | 'Active' | 'Reserved' | 'Exchanged' | 'Archived'
export const sorts = ['Distance', 'Newest', 'Title'] as const
export type Sort = (typeof sorts)[number]

export type Book = {
  id: string
  isbn: string | null
  title: string
  authors: string[]
  publishedYear: number | null
  coverUrl: string | null
  source: 'OpenLibrary' | 'Manual'
}

export type PagedResult<T> = { items: T[]; page: number; pageSize: number; totalCount: number }

/** Public result: a rounded distance and an area label, never coordinates. */
export type ListingSummary = {
  id: string
  title: string
  authors: string[]
  coverUrl: string | null
  thumbnailUrl: string | null
  condition: Condition
  category: Category
  goodForBeginners: boolean
  areaLabel: string
  distanceKm: number
  distanceIsUpperBound: boolean
  ownerDisplayName: string
  isMine: boolean
  createdAt: string
}

export type ListingImage = {
  id: string
  displayUrl: string
  thumbnailUrl: string
  width: number
  height: number
}

export type ListingDetails = {
  id: string
  book: Book
  description: string | null
  condition: Condition
  category: Category
  goodForBeginners: boolean
  status: ListingStatus
  areaLabel: string
  distance: { km: number; isUpperBound: boolean } | null
  images: ListingImage[]
  owner: { id: string; displayName: string }
  isMine: boolean
  myLocation: { areaLabel: string; latitude: number; longitude: number } | null
  createdAt: string
}

export type MyListingItem = {
  id: string
  title: string
  authors: string[]
  coverUrl: string | null
  thumbnailUrl: string | null
  condition: Condition
  category: Category
  goodForBeginners: boolean
  status: ListingStatus
  areaLabel: string
  imageCount: number
  createdAt: string
}

export type Origin = { lat: number; lng: number }

export type SearchParams = {
  origin: Origin | null
  q: string
  radiusKm: number
  condition: Condition | ''
  category: Category | ''
  beginners: boolean
  sort: Sort
}

export type ManualBook = { isbn: string | null; title: string; authors: string[] }

export type CreateListing = {
  bookId?: string
  manualBook?: ManualBook
  condition: Condition
  category: Category
  goodForBeginners: boolean
  description: string | null
}

export type UpdateListing = {
  condition: Condition
  category: Category
  goodForBeginners: boolean
  description: string | null
}

function query(values: Record<string, string | number | null | undefined>) {
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(values)) {
    if (value !== null && value !== undefined && value !== '') params.set(key, String(value))
  }
  return params.toString()
}

export const listingsApi = {
  lookupIsbn: (isbn: string, signal?: AbortSignal) =>
    apiRequest<Book>(`/api/books/lookup?${query({ isbn })}`, { signal }),

  search: (params: SearchParams, page: number, signal?: AbortSignal) =>
    apiRequest<PagedResult<ListingSummary>>(
      `/api/listings?${query({
        lat: params.origin?.lat,
        lng: params.origin?.lng,
        q: params.q.trim(),
        radiusKm: params.radiusKm,
        condition: params.condition,
        category: params.category,
        beginners: params.beginners ? 'true' : null,
        sort: params.sort,
        page,
        pageSize: 20,
      })}`,
      { signal },
    ),

  get: (id: string, origin: Origin | null, signal?: AbortSignal) =>
    apiRequest<ListingDetails>(
      `/api/listings/${id}${origin ? `?${query({ lat: origin.lat, lng: origin.lng })}` : ''}`,
      { signal },
    ),

  mine: (signal?: AbortSignal) => apiRequest<MyListingItem[]>('/api/listings/mine', { signal }),

  create: (body: CreateListing) =>
    apiRequest<ListingDetails>('/api/listings', { method: 'POST', body }),

  update: (id: string, body: UpdateListing) =>
    apiRequest<ListingDetails>(`/api/listings/${id}`, { method: 'PUT', body }),

  archive: (id: string) => apiRequest<void>(`/api/listings/${id}/archive`, { method: 'POST' }),

  uploadImage: (id: string, file: File) => {
    const form = new FormData()
    form.append('file', file)
    return apiRequest<ListingImage>(`/api/listings/${id}/images`, { method: 'POST', body: form })
  },

  removeImage: (id: string, imageId: string) =>
    apiRequest<void>(`/api/listings/${id}/images/${imageId}`, { method: 'DELETE' }),
}

export const listingKeys = {
  all: ['listings'] as const,
  search: (params: SearchParams) => ['listings', 'search', params] as const,
  detail: (id: string) => ['listings', 'detail', id] as const,
  mine: ['listings', 'mine'] as const,
}

/** Client-side mirror of the server limits (BookExchange.Domain). */
export const listingLimits = {
  maxImages: 8,
  maxImageBytes: 5 * 1024 * 1024,
  descriptionMax: 2000,
  titleMax: 300,
  radiusOptions: [2, 5, 10, 25, 50] as const,
  defaultRadiusKm: 5,
}
