import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { Link } from 'react-router'
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
import { limits, registerSchema } from './schemas'

type RegisterValues = { displayName: string; email: string; password: string }

export function RegisterPage() {
  const { t, language } = useI18n()
  const fieldMessage = useFieldMessage()
  const errorMessage = useErrorMessage()

  const form = useForm<RegisterValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: { displayName: '', email: '', password: '' },
  })
  // Emails go out in the language the person is using right now.
  const register = useMutation({
    mutationFn: (values: RegisterValues) =>
      authApi.register({ ...values, preferredLanguage: language }),
  })

  if (register.isSuccess) {
    return (
      <AuthCard title={t('register.checkEmailTitle')}>
        <p>{t('register.checkEmailBody', { email: register.variables.email })}</p>
      </AuthCard>
    )
  }

  const { errors } = form.formState
  return (
    <AuthCard title={t('register.title')}>
      <form
        noValidate
        onSubmit={form.handleSubmit((values) => register.mutate(values))}
        className="space-y-4"
      >
        {register.isError && <Alert kind="error">{errorMessage(register.error)}</Alert>}
        <TextField
          label={t('form.displayName')}
          autoComplete="nickname"
          error={fieldMessage(errors.displayName?.message)}
          {...form.register('displayName')}
        />
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
          autoComplete="new-password"
          hint={t('register.passwordHint', { min: limits.passwordMin })}
          error={fieldMessage(errors.password?.message)}
          {...form.register('password')}
        />
        <Button type="submit" className="w-full" disabled={register.isPending}>
          {register.isPending ? t('register.submitting') : t('register.submit')}
        </Button>
      </form>
      <p className="mt-6 text-sm">
        {t('register.hasAccount')}{' '}
        <Link to="/login" className="text-primary underline underline-offset-2">
          {t('nav.login')}
        </Link>
      </p>
    </AuthCard>
  )
}
