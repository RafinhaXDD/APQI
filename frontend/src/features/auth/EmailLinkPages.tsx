import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation } from '@tanstack/react-query'
import { useEffect, useRef } from 'react'
import { useForm } from 'react-hook-form'
import { Link, useSearchParams } from 'react-router'
import {
  Alert,
  AuthCard,
  Button,
  TextField,
  useErrorMessage,
  useFieldMessage,
} from '../../components/forms'
import { useI18n } from '../../i18n/context'
import { authApi } from '../../services/endpoints'
import { emailSchema, resetPasswordSchema } from './schemas'

function useLinkParams() {
  const [params] = useSearchParams()
  const userId = params.get('userId')
  const token = params.get('token')
  return userId && token ? { userId, token } : null
}

/** Opened from the confirmation email: confirms once, on arrival. */
export function ConfirmEmailPage() {
  const { t } = useI18n()
  const errorMessage = useErrorMessage()
  const link = useLinkParams()
  const confirm = useMutation({ mutationFn: authApi.confirmEmail })
  const started = useRef(false)

  useEffect(() => {
    // Guard against React StrictMode's double effect: the link should be used once.
    if (link && !started.current) {
      started.current = true
      confirm.mutate(link)
    }
  }, [link, confirm])

  return (
    <AuthCard title={t('confirm.title')}>
      {confirm.isPending && <p>{t('confirm.working')}</p>}
      {confirm.isSuccess && (
        <div className="space-y-4">
          <Alert kind="success">
            <strong>{t('confirm.success')}</strong> {t('confirm.successCredit')}
          </Alert>
          <Link
            to="/login"
            className="font-display bg-primary text-surface inline-block rounded-md px-4 py-2 font-semibold"
          >
            {t('confirm.goLogin')}
          </Link>
        </div>
      )}
      {(confirm.isError || !link) && (
        <div className="space-y-6">
          <Alert kind="error">
            {link ? errorMessage(confirm.error) : t('errors.auth.invalid_link')}
          </Alert>
          <EmailForm
            title={t('confirm.resendTitle')}
            submitLabel={t('confirm.resendSubmit')}
            doneMessage={t('login.resent')}
            send={authApi.resendConfirmation}
          />
        </div>
      )}
    </AuthCard>
  )
}

export function ForgotPasswordPage() {
  const { t } = useI18n()
  return (
    <AuthCard title={t('forgot.title')}>
      <EmailForm
        title={t('forgot.body')}
        submitLabel={t('forgot.submit')}
        doneMessage={t('forgot.sent')}
        send={authApi.forgotPassword}
      />
    </AuthCard>
  )
}

type ResetValues = { newPassword: string; confirmPassword: string }

export function ResetPasswordPage() {
  const { t } = useI18n()
  const fieldMessage = useFieldMessage()
  const errorMessage = useErrorMessage()
  const link = useLinkParams()
  const form = useForm<ResetValues>({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: { newPassword: '', confirmPassword: '' },
  })
  const reset = useMutation({
    mutationFn: (values: ResetValues) =>
      authApi.resetPassword({ ...link!, newPassword: values.newPassword }),
  })

  if (!link) {
    return (
      <AuthCard title={t('reset.title')}>
        <Alert kind="error">{t('reset.missingLink')}</Alert>
      </AuthCard>
    )
  }

  if (reset.isSuccess) {
    return (
      <AuthCard title={t('reset.title')}>
        <div className="space-y-4">
          <Alert kind="success">{t('reset.success')}</Alert>
          <Link
            to="/login"
            className="font-display bg-primary text-surface inline-block rounded-md px-4 py-2 font-semibold"
          >
            {t('reset.goLogin')}
          </Link>
        </div>
      </AuthCard>
    )
  }

  const { errors } = form.formState
  return (
    <AuthCard title={t('reset.title')}>
      <form
        noValidate
        onSubmit={form.handleSubmit((values) => reset.mutate(values))}
        className="space-y-4"
      >
        {reset.isError && <Alert kind="error">{errorMessage(reset.error)}</Alert>}
        <TextField
          label={t('form.newPassword')}
          type="password"
          autoComplete="new-password"
          error={fieldMessage(errors.newPassword?.message)}
          {...form.register('newPassword')}
        />
        <TextField
          label={t('form.confirmPassword')}
          type="password"
          autoComplete="new-password"
          error={fieldMessage(errors.confirmPassword?.message)}
          {...form.register('confirmPassword')}
        />
        <Button type="submit" className="w-full" disabled={reset.isPending}>
          {reset.isPending ? t('common.saving') : t('reset.submit')}
        </Button>
      </form>
    </AuthCard>
  )
}

/** One email field; always shows the same neutral confirmation, whatever the server knows (R-19). */
function EmailForm({
  title,
  submitLabel,
  doneMessage,
  send,
}: {
  title: string
  submitLabel: string
  doneMessage: string
  send: (email: string) => Promise<void>
}) {
  const { t } = useI18n()
  const fieldMessage = useFieldMessage()
  const errorMessage = useErrorMessage()
  const form = useForm<{ email: string }>({
    resolver: zodResolver(emailSchema),
    defaultValues: { email: '' },
  })
  const mutation = useMutation({ mutationFn: (values: { email: string }) => send(values.email) })

  if (mutation.isSuccess) return <Alert kind="info">{doneMessage}</Alert>

  return (
    <form
      noValidate
      onSubmit={form.handleSubmit((values) => mutation.mutate(values))}
      className="space-y-4"
    >
      <p>{title}</p>
      {mutation.isError && <Alert kind="error">{errorMessage(mutation.error)}</Alert>}
      <TextField
        label={t('form.email')}
        type="email"
        autoComplete="email"
        error={fieldMessage(form.formState.errors.email?.message)}
        {...form.register('email')}
      />
      <Button type="submit" className="w-full" disabled={mutation.isPending}>
        {submitLabel}
      </Button>
    </form>
  )
}
