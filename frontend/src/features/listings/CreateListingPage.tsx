import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'
import { Alert, Button, Card, TextField, useErrorMessage } from '../../components/forms'
import { useI18n } from '../../i18n/context'
import { ApiError } from '../../services/api'
import { profileApi, queryKeys } from '../../services/endpoints'
import {
  categories,
  conditions,
  listingKeys,
  listingLimits,
  listingsApi,
  type Book,
  type Category,
  type Condition,
  type ManualBook,
} from '../../services/listings'
import { BarcodeScanner, type CameraProblem } from './BarcodeScanner'
import { BookCover } from './components'
import { normalizeIsbn } from './isbn'

type Photo = { file: File; preview: string }

/** CLAUDE.md: listing must take ~30 s: scan → auto-filled metadata → photo → publish. */
export function CreateListingPage() {
  const { t } = useI18n()
  const errorMessage = useErrorMessage()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const profile = useQuery({
    queryKey: queryKeys.profile,
    queryFn: ({ signal }) => profileApi.get(signal),
  })
  const [isbnInput, setIsbnInput] = useState('')
  const [isbnError, setIsbnError] = useState<string | null>(null)
  const [scanning, setScanning] = useState(false)
  const [cameraProblem, setCameraProblem] = useState<CameraProblem | null>(null)
  const [book, setBook] = useState<Book | null>(null)
  const [manual, setManual] = useState<ManualBook | null>(null)
  const [manualTitleError, setManualTitleError] = useState(false)
  const [condition, setCondition] = useState<Condition>('Good')
  const [category, setCategory] = useState<Category | null>(null)
  const [categoryError, setCategoryError] = useState(false)
  const [goodForBeginners, setGoodForBeginners] = useState(false)
  const [description, setDescription] = useState('')
  const [photos, setPhotos] = useState<Photo[]>([])
  const [photoError, setPhotoError] = useState<string | null>(null)
  const [progress, setProgress] = useState<{ n: number; total: number } | null>(null)
  const [failedUpload, setFailedUpload] = useState<{ id: string; reason: string } | null>(null)
  // Free preview URLs when the page goes away.
  const photosRef = useRef(photos)
  useEffect(() => {
    photosRef.current = photos
  }, [photos])
  useEffect(() => () => photosRef.current.forEach((p) => URL.revokeObjectURL(p.preview)), [])

  const lookup = useMutation({
    mutationFn: (isbn: string) => listingsApi.lookupIsbn(isbn),
    onSuccess: (found) => {
      setBook(found)
      setManual(null)
    },
    onError: (error, isbn) => {
      // Not found or provider down: never block listing, switch to manual entry with the ISBN kept.
      if (
        error instanceof ApiError &&
        (error.code === 'book.not_found' || error.code === 'book.lookup_unavailable')
      ) {
        setManual({ isbn, title: '', authors: [] })
      }
    },
  })

  const { mutate: lookupIsbn } = lookup
  const runLookup = useCallback(
    (raw: string) => {
      const isbn = normalizeIsbn(raw)
      if (!isbn) {
        setIsbnError(t('errors.book.invalid_isbn'))
        return
      }
      setIsbnError(null)
      setIsbnInput(isbn)
      lookupIsbn(isbn)
    },
    [t, lookupIsbn],
  )

  const onDetected = useCallback(
    (isbn: string) => {
      setScanning(false)
      runLookup(isbn)
    },
    [runLookup],
  )
  const onCameraError = useCallback((problem: CameraProblem) => {
    setScanning(false)
    setCameraProblem(problem)
  }, [])

  const addPhotos = (files: FileList | null) => {
    if (!files) return
    setPhotoError(null)
    const accepted: Photo[] = []
    for (const file of Array.from(files)) {
      if (file.size > listingLimits.maxImageBytes) {
        setPhotoError(t('errors.image.too_large'))
        continue
      }
      accepted.push({ file, preview: URL.createObjectURL(file) })
    }
    setPhotos((current) => {
      const next = [...current, ...accepted]
      next.slice(listingLimits.maxImages).forEach((p) => URL.revokeObjectURL(p.preview))
      return next.slice(0, listingLimits.maxImages)
    })
  }

  const removePhoto = (index: number) =>
    setPhotos((current) => {
      URL.revokeObjectURL(current[index].preview)
      return current.filter((_, i) => i !== index)
    })

  const publish = useMutation({
    mutationFn: async () => {
      const created = await listingsApi.create({
        ...(book ? { bookId: book.id } : { manualBook: manual! }),
        condition,
        category: category!,
        goodForBeginners,
        description: description.trim() || null,
      })
      for (const [index, photo] of photos.entries()) {
        setProgress({ n: index + 1, total: photos.length })
        try {
          await listingsApi.uploadImage(created.id, photo.file)
        } catch (error) {
          // The listing exists; report the photo problem instead of losing the listing.
          return { created, failure: errorMessage(error) }
        }
      }
      return { created, failure: null }
    },
    onSuccess: ({ created, failure }) => {
      void queryClient.invalidateQueries({ queryKey: listingKeys.all })
      setProgress(null)
      if (failure) setFailedUpload({ id: created.id, reason: failure })
      else navigate(`/listings/${created.id}`)
    },
  })

  const onPublish = (event: FormEvent) => {
    event.preventDefault()
    if (manual && !manual.title.trim()) {
      setManualTitleError(true)
      return
    }
    if (!category) {
      setCategoryError(true)
      return
    }
    publish.mutate()
  }

  const homeArea = profile.data?.homeArea
  const bookChosen = book !== null || manual !== null

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <h1 className="text-2xl font-semibold">{t('create.title')}</h1>

      {profile.isSuccess && !homeArea && (
        <Alert kind="info">
          {t('create.needHome')}{' '}
          <Link to="/profile" className="font-semibold underline underline-offset-2">
            {t('create.goProfile')}
          </Link>
        </Alert>
      )}

      <Card title={t('create.isbnStep')}>
        {!bookChosen ? (
          <div className="space-y-4">
            <form
              noValidate
              className="flex flex-col gap-3 sm:flex-row sm:items-end"
              onSubmit={(event) => {
                event.preventDefault()
                runLookup(isbnInput)
              }}
            >
              <TextField
                className="flex-1"
                label={t('create.isbnLabel')}
                hint={t('create.isbnHint')}
                inputMode="numeric"
                autoComplete="off"
                value={isbnInput}
                error={isbnError ?? undefined}
                onChange={(event) => setIsbnInput(event.target.value)}
              />
              <Button type="submit" disabled={lookup.isPending} className="sm:mb-6">
                {lookup.isPending ? t('create.looking') : t('create.lookup')}
              </Button>
            </form>
            {lookup.isError && !manual && <Alert kind="error">{errorMessage(lookup.error)}</Alert>}
            {cameraProblem && <Alert kind="info">{t(`create.cameraError.${cameraProblem}`)}</Alert>}
            {scanning ? (
              <div className="space-y-2">
                <BarcodeScanner onDetected={onDetected} onError={onCameraError} />
                <Button type="button" variant="link" onClick={() => setScanning(false)}>
                  {t('create.stopScan')}
                </Button>
              </div>
            ) : (
              <div className="flex flex-wrap gap-4">
                <Button
                  type="button"
                  variant="secondary"
                  onClick={() => {
                    setCameraProblem(null)
                    setScanning(true)
                  }}
                >
                  {t('create.scan')}
                </Button>
                <Button
                  type="button"
                  variant="link"
                  onClick={() => setManual({ isbn: null, title: '', authors: [] })}
                >
                  {t('create.noIsbn')}
                </Button>
              </div>
            )}
          </div>
        ) : (
          <div className="space-y-4">
            {book && (
              <div className="flex gap-4">
                <BookCover
                  src={book.coverUrl}
                  title={book.title}
                  className="h-32 w-24 shrink-0 rounded"
                />
                <div>
                  <p className="text-lg font-semibold">{book.title}</p>
                  <p>{book.authors.join(', ')}</p>
                  {book.isbn && (
                    <p className="text-text-muted text-sm">
                      {t('listing.isbn', { isbn: book.isbn })}
                    </p>
                  )}
                </div>
              </div>
            )}
            {manual && (
              <div className="space-y-4">
                {lookup.isError && <Alert kind="info">{errorMessage(lookup.error)}</Alert>}
                <TextField
                  label={t('create.bookTitle')}
                  value={manual.title}
                  maxLength={listingLimits.titleMax}
                  error={manualTitleError ? t('validation.required') : undefined}
                  onChange={(event) => {
                    setManualTitleError(false)
                    setManual({ ...manual, title: event.target.value })
                  }}
                />
                <TextField
                  label={t('create.authors')}
                  defaultValue={manual.authors.join(', ')}
                  onChange={(event) =>
                    setManual({
                      ...manual,
                      authors: event.target.value
                        .split(',')
                        .map((a) => a.trim())
                        .filter(Boolean),
                    })
                  }
                />
              </div>
            )}
            <Button
              type="button"
              variant="link"
              onClick={() => {
                setBook(null)
                setManual(null)
                lookup.reset()
              }}
            >
              {t('create.changeBook')}
            </Button>
          </div>
        )}
      </Card>

      {bookChosen && (
        <form noValidate onSubmit={onPublish} className="space-y-6">
          <Card title={t('create.detailsStep')}>
            <fieldset className="mb-6 space-y-3">
              <legend className="mb-2 text-sm font-medium">{t('create.category')}</legend>
              <div className="flex flex-wrap gap-2">
                {categories.map((value) => (
                  <label
                    key={value}
                    className={`cursor-pointer rounded-full border px-3 py-1.5 text-sm ${
                      category === value
                        ? 'bg-primary text-surface border-primary font-semibold'
                        : 'border-border'
                    }`}
                  >
                    <input
                      type="radio"
                      name="category"
                      value={value}
                      checked={category === value}
                      onChange={() => {
                        setCategory(value)
                        setCategoryError(false)
                      }}
                      className="sr-only"
                    />
                    {t(`category.${value}`)}
                  </label>
                ))}
              </div>
              {categoryError && (
                <p role="alert" className="text-error text-sm">
                  {t('create.categoryRequired')}
                </p>
              )}
              <label className="flex items-start gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={goodForBeginners}
                  onChange={(event) => setGoodForBeginners(event.target.checked)}
                  className="accent-primary mt-0.5 h-4 w-4"
                />
                {t('create.beginners')}
              </label>
            </fieldset>
            <fieldset className="space-y-4">
              <legend className="mb-2 text-sm font-medium">{t('search.condition')}</legend>
              <div className="flex flex-wrap gap-2">
                {conditions.map((value) => (
                  <label
                    key={value}
                    className={`cursor-pointer rounded-full border px-3 py-1.5 text-sm ${
                      condition === value
                        ? 'bg-secondary text-ink border-secondary font-semibold'
                        : 'border-border'
                    }`}
                  >
                    <input
                      type="radio"
                      name="condition"
                      value={value}
                      checked={condition === value}
                      onChange={() => setCondition(value)}
                      className="sr-only"
                    />
                    {t(`condition.${value}`)}
                  </label>
                ))}
              </div>
              <div>
                <label htmlFor="description" className="mb-1 block text-sm font-medium">
                  {t('create.description')}
                </label>
                <textarea
                  id="description"
                  rows={3}
                  maxLength={listingLimits.descriptionMax}
                  value={description}
                  onChange={(event) => setDescription(event.target.value)}
                  className="border-border bg-surface w-full rounded-md border px-3 py-2"
                />
                <p className="text-text-muted mt-1 text-sm">{t('create.descriptionHint')}</p>
              </div>
            </fieldset>
          </Card>

          <Card title={t('create.photosStep', { max: listingLimits.maxImages })}>
            <div className="space-y-3">
              {photoError && <Alert kind="error">{photoError}</Alert>}
              {photos.length > 0 && (
                <ul className="grid grid-cols-4 gap-2">
                  {photos.map((photo, index) => (
                    <li key={photo.preview} className="relative">
                      <img
                        src={photo.preview}
                        alt=""
                        className="aspect-square w-full rounded object-cover"
                      />
                      <button
                        type="button"
                        onClick={() => removePhoto(index)}
                        aria-label={t('create.removePhoto')}
                        className="bg-text text-surface absolute top-1 right-1 rounded-full px-2 text-sm"
                      >
                        ×
                      </button>
                    </li>
                  ))}
                </ul>
              )}
              {photos.length < listingLimits.maxImages && (
                <label className="font-display bg-secondary text-ink inline-block cursor-pointer rounded-md px-4 py-2 font-semibold">
                  {t('create.addPhotos')}
                  <input
                    type="file"
                    accept="image/jpeg,image/png,image/webp"
                    multiple
                    className="sr-only"
                    onChange={(event) => {
                      addPhotos(event.target.files)
                      event.target.value = ''
                    }}
                  />
                </label>
              )}
            </div>
          </Card>

          {homeArea && (
            <p className="text-sm">{t('create.locationNote', { area: homeArea.label })}</p>
          )}
          {publish.isError && <Alert kind="error">{errorMessage(publish.error)}</Alert>}
          {failedUpload && (
            <Alert kind="error">
              {t('create.photoFailed', { reason: failedUpload.reason })}{' '}
              <Link
                to={`/listings/${failedUpload.id}`}
                className="font-semibold underline underline-offset-2"
              >
                {t('edit.done')}
              </Link>
            </Alert>
          )}
          {!failedUpload && (
            <Button type="submit" className="w-full" disabled={publish.isPending || !homeArea}>
              {progress
                ? t('create.uploading', progress)
                : publish.isPending
                  ? t('create.publishing')
                  : t('create.publish')}
            </Button>
          )}
        </form>
      )}
    </div>
  )
}
