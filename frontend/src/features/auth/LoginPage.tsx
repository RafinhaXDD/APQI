import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { Link, useLocation, useNavigate } from 'react-router'
import {
  Alert,
  AuthCard,
  Button,
  TextField,
  useErrorMessage,
  useFieldMessage,
} from '../../components/forms'
import { useI18n } from '../../i18n/context'
import { ApiError } from '../../services/api'
import { authApi } from '../../services/endpoints'
import { useAuth } from './context'
import { loginSchema } from './schemas'

type LoginValues = { email: string; password: string }

export function LoginPage() {
  const { t } = useI18n()
  const fieldMessage = useFieldMessage()
  const errorMessage = useErrorMessage()
  const { login } = useAuth()
  const navigate = useNavigate()
  const from = (useLocation().state as { from?: string } | null)?.from ?? '/profile'

  const form = useForm<LoginValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  })
  const signIn = useMutation({
    mutationFn: (values: LoginValues) => login(values.email, values.password),
    onSuccess: () => navigate(from, { replace: true }),
  })
  const resend = useMutation({ mutationFn: (email: string) => authApi.resendConfirmation(email) })

  const notConfirmed =
    signIn.error instanceof ApiError && signIn.error.code === 'auth.email_not_confirmed'
  const { errors } = form.formState

  return (
    <AuthCard title={t('login.title')}>
      <form
        noValidate
        onSubmit={form.handleSubmit((values) => signIn.mutate(values))}
        className="space-y-4"
      >
        {signIn.isError && <Alert kind="error">{errorMessage(signIn.error)}</Alert>}
        {notConfirmed &&
          (resend.isSuccess ? (
            <Alert kind="info">{t('login.resent')}</Alert>
          ) : (
            <Button
              type="button"
              variant="link"
              disabled={resend.isPending}
              onClick={() => resend.mutate(form.getValues('email'))}
            >
              {t('login.resend')}
            </Button>
          ))}

        <TextField
          label={t('form.email')}
          type="email"
          autoComplete="email"
          error={fieldMessage(errors.email?.message)}
          {...form.register('email')}
        />
        <TextField
          label={t('form.password')}
          type="password"
          autoComplete="current-password"
          error={fieldMessage(errors.password?.message)}
          {...form.register('password')}
        />

        <Button type="submit" className="w-full" disabled={signIn.isPending}>
          {signIn.isPending ? t('login.submitting') : t('login.submit')}
        </Button>
      </form>

      <div className="mt-6 space-y-2 text-sm">
        <p>
          <Link to="/forgot-password" className="text-primary underline underline-offset-2">
            {t('login.forgot')}
          </Link>
        </p>
        <p>
          {t('login.noAccount')}{' '}
          <Link to="/register" className="text-primary underline underline-offset-2">
            {t('nav.register')}
          </Link>
        </p>
      </div>
    </AuthCard>
  )
}
