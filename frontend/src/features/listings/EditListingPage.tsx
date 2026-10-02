import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router'
import { Alert, Button, Card, useErrorMessage } from '../../components/forms'
import { useI18n } from '../../i18n/context'
import {
  categories,
  conditions,
  listingKeys,
  listingLimits,
  listingsApi,
  type Category,
  type Condition,
  type ListingDetails,
} from '../../services/listings'

export function EditListingPage() {
  const { id = '' } = useParams()
  const { t } = useI18n()
  const errorMessage = useErrorMessage()
  const listing = useQuery({
    queryKey: listingKeys.detail(id),
    queryFn: ({ signal }) => listingsApi.get(id, null, signal),
  })

  if (listing.isPending) return <p className="text-text-muted">{t('common.loading')}</p>
  if (listing.isError) return <Alert kind="error">{errorMessage(listing.error)}</Alert>
  return <EditForm listing={listing.data} />
}

function EditForm({ listing }: { listing: ListingDetails }) {
  const { t } = useI18n()
  const errorMessage = useErrorMessage()
  const queryClient = useQueryClient()
  const [condition, setCondition] = useState<Condition>(listing.condition)
  const [category, setCategory] = useState<Category>(listing.category)
  const [goodForBeginners, setGoodForBeginners] = useState(listing.goodForBeginners)
  const [description, setDescription] = useState(listing.description ?? '')

  const refresh = (updated?: ListingDetails) => {
    if (updated) queryClient.setQueryData(listingKeys.detail(listing.id), updated)
    void queryClient.invalidateQueries({ queryKey: listingKeys.all })
  }
  const save = useMutation({
    mutationFn: () =>
      listingsApi.update(listing.id, {
        condition,
        category,
        goodForBeginners,
        description: description.trim() || null,
      }),
    onSuccess: refresh,
  })
  const upload = useMutation({
    mutationFn: (file: File) => listingsApi.uploadImage(listing.id, file),
    onSuccess: () => refresh(),
  })
  const remove = useMutation({
    mutationFn: (imageId: string) => listingsApi.removeImage(listing.id, imageId),
    onSuccess: () => refresh(),
  })

  const onSubmit = (event: FormEvent) => {
    event.preventDefault()
    save.mutate()
  }

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <h1 className="text-2xl font-semibold">{t('edit.title')}</h1>
      <p className="text-lg">{listing.book.title}</p>

      <Card title={t('create.detailsStep')}>
        <form noValidate onSubmit={onSubmit} className="space-y-4">
          {save.isError && <Alert kind="error">{errorMessage(save.error)}</Alert>}
          {save.isSuccess && <Alert kind="success">{t('edit.saved')}</Alert>}
          <div>
            <label htmlFor="condition" className="mb-1 block text-sm font-medium">
              {t('search.condition')}
            </label>
            <select
              id="condition"
              value={condition}
              onChange={(event) => setCondition(event.target.value as Condition)}
              className="border-border bg-surface rounded-md border px-3 py-2"
            >
              {conditions.map((value) => (
                <option key={value} value={value}>
                  {t(`condition.${value}`)}
                </option>
              ))}
            </select>
          </div>
          <div>
            {' '}
            <label htmlFor="category" className="mb-1 block text-sm font-medium">
              {' '}
              {t('create.category')}{' '}
            </label>{' '}
            <select
              id="category"
              value={category}
              onChange={(event) => setCategory(event.target.value as Category)}
              className="border-border bg-surface rounded-md border px-3 py-2"
            >
              {' '}
              {categories.map((value) => (
                <option key={value} value={value}>
                  {' '}
                  {t(`category.${value}`)}{' '}
                </option>
              ))}{' '}
            </select>{' '}
          </div>{' '}
          <label className="flex items-start gap-2 text-sm">
            {' '}
            <input
              type="checkbox"
              checked={goodForBeginners}
              onChange={(event) => setGoodForBeginners(event.target.checked)}
              className="accent-primary mt-0.5 h-4 w-4"
            />{' '}
            {t('create.beginners')}{' '}
          </label>
          <div>
            <label htmlFor="description" className="mb-1 block text-sm font-medium">
              {t('create.description')}
            </label>
            <textarea
              id="description"
              rows={4}
              maxLength={listingLimits.descriptionMax}
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              className="border-border bg-surface w-full rounded-md border px-3 py-2"
            />
          </div>
          <Button type="submit" disabled={save.isPending}>
            {save.isPending ? t('common.saving') : t('common.save')}
          </Button>
        </form>
      </Card>

      <Card title={t('edit.photos')}>
        <div className="space-y-3">
          {(upload.isError || remove.isError) && (
            <Alert kind="error">{errorMessage(upload.error ?? remove.error)}</Alert>
          )}
          {listing.images.length === 0 && (
            <p className="text-text-muted text-sm">{t('listing.noPhotos')}</p>
          )}
          <ul className="grid grid-cols-4 gap-2">
            {listing.images.map((image, index) => (
              <li key={image.id} className="relative">
                <img
                  src={image.thumbnailUrl}
                  alt={t('listing.photo', { n: index + 1, total: listing.images.length })}
                  className="aspect-square w-full rounded object-cover"
                />
                <button
                  type="button"
                  onClick={() => remove.mutate(image.id)}
                  disabled={remove.isPending}
                  aria-label={t('create.removePhoto')}
                  className="bg-text text-surface absolute top-1 right-1 rounded-full px-2 text-sm"
                >
                  ×
                </button>
              </li>
            ))}
          </ul>
          {listing.images.length < listingLimits.maxImages && (
            <label className="font-display bg-secondary text-ink inline-block cursor-pointer rounded-md px-4 py-2 font-semibold">
              {upload.isPending ? t('common.saving') : t('create.addPhotos')}
              <input
                type="file"
                accept="image/jpeg,image/png,image/webp"
                className="sr-only"
                disabled={upload.isPending}
                onChange={(event) => {
                  const file = event.target.files?.[0]
                  if (file) upload.mutate(file)
                  event.target.value = ''
                }}
              />
            </label>
          )}
        </div>
      </Card>

      <Link to={`/listings/${listing.id}`} className="text-primary underline underline-offset-2">
        {t('edit.done')}
      </Link>
    </div>
  )
}
