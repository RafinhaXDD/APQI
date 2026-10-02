import {
  forwardRef,
  useId,
  type ButtonHTMLAttributes,
  type InputHTMLAttributes,
  type ReactNode,
} from 'react'
import { useI18n } from '../i18n/context'
import type { MessageKey } from '../i18n/pt-BR'
import { messageParams } from '../features/auth/schemas'
import { ApiError } from '../services/api'
import { dictionaries } from '../i18n/i18n'

/** Translates a zod message (a translation key) or passes server text through. */
export function useFieldMessage() {
  const { t, language } = useI18n()
  return (message?: string) => {
    if (!message) return undefined
    return message in dictionaries[language]
      ? t(message as MessageKey, messageParams[message as MessageKey])
      : message
  }
}

/** Turns any error into a translated sentence, using the server's stable code. */
export function useErrorMessage() {
  const { t, language } = useI18n()
  return (error: unknown) => {
    const code = error instanceof ApiError ? error.code : 'server_error'
    const key = `errors.${code}` as MessageKey
    return key in dictionaries[language] ? t(key) : t('errors.server_error')
  }
}

type FieldProps = InputHTMLAttributes<HTMLInputElement> & {
  label: string
  error?: string
  hint?: string
}

export const TextField = forwardRef<HTMLInputElement, FieldProps>(function TextField(
  { label, error, hint, id, className, ...input },
  ref,
) {
  const generated = useId()
  const inputId = id ?? generated
  const hintId = hint ? `${inputId}-hint` : undefined
  const errorId = error ? `${inputId}-error` : undefined

  return (
    <div className={className}>
      <label htmlFor={inputId} className="mb-1 block text-sm font-medium">
        {label}
      </label>
      <input
        ref={ref}
        id={inputId}
        aria-invalid={error ? true : undefined}
        aria-describedby={[hintId, errorId].filter(Boolean).join(' ') || undefined}
        className={`bg-surface w-full rounded-md border px-3 py-2 text-base ${error ? 'border-error' : 'border-border'}`}
        {...input}
      />
      {hint && (
        <p id={hintId} className="text-text-muted mt-1 text-sm">
          {hint}
        </p>
      )}
      {error && (
        <p id={errorId} className="text-error mt-1 text-sm">
          {error}
        </p>
      )}
    </div>
  )
})

export function Button({
  variant = 'primary',
  className = '',
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: 'primary' | 'secondary' | 'link' }) {
  const styles = {
    primary:
      'bg-primary text-surface hover:bg-primary-dark rounded-md px-4 py-2 font-semibold disabled:opacity-60',
    secondary: 'bg-secondary text-ink rounded-md px-4 py-2 font-semibold disabled:opacity-60',
    link: 'text-primary underline underline-offset-2',
  }
  return <button className={`${styles[variant]} ${className}`} {...props} />
}

export function Alert({
  kind,
  children,
}: {
  kind: 'error' | 'success' | 'info'
  children: ReactNode
}) {
  const styles = {
    error: 'border-error text-error',
    success: 'border-success text-success',
    info: 'border-border text-text',
  }
  return (
    <div
      role={kind === 'error' ? 'alert' : 'status'}
      className={`bg-surface rounded-md border px-4 py-3 text-sm ${styles[kind]}`}
    >
      {children}
    </div>
  )
}

export function Card({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="border-border bg-surface rounded-lg border p-6">
      <h2 className="mb-4 text-lg font-semibold">{title}</h2>
      {children}
    </section>
  )
}

export function AuthCard({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div className="mx-auto w-full max-w-md">
      <section className="border-border bg-surface rounded-lg border p-6">
        <h1 className="mb-6 text-2xl font-semibold">{title}</h1>
        {children}
      </section>
    </div>
  )
}
