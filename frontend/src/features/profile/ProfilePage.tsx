import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import {
  Alert,
  Button,
  Card,
  TextField,
  useErrorMessage,
  useFieldMessage,
} from '../../components/forms'
import { useI18n } from '../../i18n/context'
import { languages, type Language } from '../../i18n/i18n'
import type { MessageKey } from '../../i18n/pt-BR'
import { authApi, profileApi, queryKeys, type MyProfile } from '../../services/endpoints'
import { changePasswordSchema, displayName, limits } from '../auth/schemas'

const msg = (key: MessageKey) => ({ message: key })

const detailsSchema = z.object({
  displayName,
  bio: z.string().max(limits.bioMax, msg('validation.bioMax')),
  preferredLanguage: z.enum(languages),
})

const homeSchema = z.object({
  label: z
    .string()
    .trim()
    .min(1, msg('validation.areaLabel'))
    .max(limits.areaLabelMax, msg('validation.areaLabel')),
  latitude: z.coerce
    .number<string>(msg('validation.latitude'))
    .min(-90, msg('validation.latitude'))
    .max(90, msg('validation.latitude')),
  longitude: z.coerce
    .number<string>(msg('validation.longitude'))
    .min(-180, msg('validation.longitude'))
    .max(180, msg('validation.longitude')),
})

export function ProfilePage() {
  const { t, tPlural } = useI18n()
  const errorMessage = useErrorMessage()
  const profile = useQuery({
    queryKey: queryKeys.profile,
    queryFn: ({ signal }) => profileApi.get(signal),
  })

  if (profile.isPending) return <p className="text-text-muted">{t('common.loading')}</p>
  if (profile.isError) return <Alert kind="error">{errorMessage(profile.error)}</Alert>

  const { credits } = profile.data
  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <h1 className="text-2xl font-semibold">{t('profile.title')}</h1>

      <section className="border-border bg-surface rounded-lg border p-6">
        <p className="flex items-center gap-3 text-lg font-semibold">
          <span className="bg-accent text-text rounded-full px-3 py-1 text-base">
            {credits.available}
          </span>
          {tPlural('profile.credits', credits.available)}
        </p>
        {credits.held > 0 && (
          <p className="text-text-muted mt-1 text-sm">
            {t('profile.creditsHeld', { count: credits.held })}
          </p>
        )}
        <p className="text-text-muted mt-2 text-sm">{t('profile.creditsHelp')}</p>
      </section>

      <DetailsForm profile={profile.data} />
      <HomeAreaForm profile={profile.data} />
      <ChangePasswordForm />
    </div>
  )
}

function useSaveProfile<T>(mutationFn: (values: T) => Promise<MyProfile>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn,
    onSuccess: (updated) => {
      queryClient.setQueryData(queryKeys.profile, updated)
      void queryClient.invalidateQueries({ queryKey: queryKeys.me })
    },
  })
}

function DetailsForm({ profile }: { profile: MyProfile }) {
  const { t } = useI18n()
  const fieldMessage = useFieldMessage()
  const errorMessage = useErrorMessage()
  const form = useForm<z.infer<typeof detailsSchema>>({
    resolver: zodResolver(detailsSchema),
    defaultValues: {
      displayName: profile.displayName,
      bio: profile.bio ?? '',
      preferredLanguage: profile.preferredLanguage,
    },
  })
  const save = useSaveProfile((values: z.infer<typeof detailsSchema>) =>
    profileApi.update({ ...values, bio: values.bio.trim() || null }),
  )
  const { errors } = form.formState

  return (
    <Card title={t('profile.detailsTitle')}>
      <form
        noValidate
        onSubmit={form.handleSubmit((values) => save.mutate(values))}
        className="space-y-4"
      >
        {save.isError && <Alert kind="error">{errorMessage(save.error)}</Alert>}
        {save.isSuccess && <Alert kind="success">{t('common.saved')}</Alert>}
        <TextField
          label={t('form.displayName')}
          error={fieldMessage(errors.displayName?.message)}
          {...form.register('displayName')}
        />
        <div>
          <label htmlFor="bio" className="mb-1 block text-sm font-medium">
            {t('form.bio')}
          </label>
          <textarea
            id="bio"
            rows={3}
            aria-invalid={errors.bio ? true : undefined}
            className="border-border bg-surface w-full rounded-md border px-3 py-2"
            {...form.register('bio')}
          />
          {errors.bio && (
            <p className="text-error mt-1 text-sm">{fieldMessage(errors.bio.message)}</p>
          )}
        </div>
        <div>
          <label htmlFor="preferredLanguage" className="mb-1 block text-sm font-medium">
            {t('form.emailLanguage')}
          </label>
          <select
            id="preferredLanguage"
            className="border-border bg-surface rounded-md border px-3 py-2"
            {...form.register('preferredLanguage')}
          >
            {languages.map((language: Language) => (
              <option key={language} value={language}>
                {t(`lang.${language}`)}
              </option>
            ))}
          </select>
        </div>
        <Button type="submit" disabled={save.isPending}>
          {save.isPending ? t('common.saving') : t('common.save')}
        </Button>
      </form>
    </Card>
  )
}

type HomeInput = { label: string; latitude: string; longitude: string }

function HomeAreaForm({ profile }: { profile: MyProfile }) {
  const { t } = useI18n()
  const fieldMessage = useFieldMessage()
  const errorMessage = useErrorMessage()
  const [locating, setLocating] = useState(false)
  const [locationError, setLocationError] = useState(false)
  const home = profile.homeArea
  const form = useForm<HomeInput, unknown, z.infer<typeof homeSchema>>({
    resolver: zodResolver(homeSchema),
    defaultValues: {
      label: home?.label ?? '',
      latitude: home ? String(home.latitude) : '',
      longitude: home ? String(home.longitude) : '',
    },
  })
  const save = useSaveProfile(profileApi.setHomeArea)
  const { errors } = form.formState

  const useMyLocation = () => {
    if (!('geolocation' in navigator)) {
      setLocationError(true)
      return
    }
    setLocating(true)
    setLocationError(false)
    navigator.geolocation.getCurrentPosition(
      ({ coords }) => {
        form.setValue('latitude', coords.latitude.toFixed(5), { shouldValidate: true })
        form.setValue('longitude', coords.longitude.toFixed(5), { shouldValidate: true })
        setLocating(false)
      },
      () => {
        setLocationError(true)
        setLocating(false)
      },
      { enableHighAccuracy: false, timeout: 10_000 },
    )
  }

  return (
    <Card title={t('profile.homeTitle')}>
      <p className="text-text-muted mb-4 text-sm">{t('profile.homeBody')}</p>
      {!home && <p className="mb-4 text-sm">{t('profile.homeNotSet')}</p>}
      <form
        noValidate
        onSubmit={form.handleSubmit((values) => save.mutate(values))}
        className="space-y-4"
      >
        {save.isError && <Alert kind="error">{errorMessage(save.error)}</Alert>}
        {save.isSuccess && <Alert kind="success">{t('common.saved')}</Alert>}
        {locationError && <Alert kind="info">{t('profile.locationDenied')}</Alert>}
        <TextField
          label={t('profile.homeLabel')}
          error={fieldMessage(errors.label?.message)}
          {...form.register('label')}
        />
        <Button type="button" variant="link" onClick={useMyLocation} disabled={locating}>
          {locating ? t('profile.locating') : t('profile.useLocation')}
        </Button>
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <TextField
            label={t('profile.latitude')}
            inputMode="decimal"
            error={fieldMessage(errors.latitude?.message)}
            {...form.register('latitude')}
          />
          <TextField
            label={t('profile.longitude')}
            inputMode="decimal"
            error={fieldMessage(errors.longitude?.message)}
            {...form.register('longitude')}
          />
        </div>
        <Button type="submit" disabled={save.isPending}>
          {save.isPending ? t('common.saving') : t('common.save')}
        </Button>
      </form>
    </Card>
  )
}

type PasswordValues = { currentPassword: string; newPassword: string }

function ChangePasswordForm() {
  const { t } = useI18n()
  const fieldMessage = useFieldMessage()
  const errorMessage = useErrorMessage()
  const form = useForm<PasswordValues>({
    resolver: zodResolver(changePasswordSchema),
    defaultValues: { currentPassword: '', newPassword: '' },
  })
  const change = useMutation({ mutationFn: authApi.changePassword, onSuccess: () => form.reset() })
  const { errors } = form.formState

  return (
    <Card title={t('profile.passwordTitle')}>
      <form
        noValidate
        onSubmit={form.handleSubmit((values) => change.mutate(values))}
        className="space-y-4"
      >
        {change.isError && <Alert kind="error">{errorMessage(change.error)}</Alert>}
        {change.isSuccess && <Alert kind="success">{t('profile.passwordChanged')}</Alert>}
        <TextField
          label={t('form.currentPassword')}
          type="password"
          autoComplete="current-password"
          error={fieldMessage(errors.currentPassword?.message)}
          {...form.register('currentPassword')}
        />
        <TextField
          label={t('form.newPassword')}
          type="password"
          autoComplete="new-password"
          error={fieldMessage(errors.newPassword?.message)}
          {...form.register('newPassword')}
        />
        <Button type="submit" disabled={change.isPending}>
          {change.isPending ? t('common.saving') : t('common.save')}
        </Button>
      </form>
    </Card>
  )
}
