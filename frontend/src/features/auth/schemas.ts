import { z } from 'zod'
import type { MessageKey } from '../../i18n/pt-BR'

/**
 * Client rules mirror the server validators (BookExchange.Application) so most mistakes are caught
 * before a round trip. The server stays the authority. Messages are translation keys.
 */
export const limits = {
  passwordMin: 10,
  passwordMax: 128,
  displayNameMin: 2,
  displayNameMax: 50,
  bioMax: 500,
  areaLabelMax: 80,
} as const

const msg = (key: MessageKey) => ({ message: key })

export const email = z
  .string()
  .trim()
  .min(1, msg('validation.required'))
  .email(msg('validation.email'))

export const newPassword = z
  .string()
  .min(limits.passwordMin, msg('validation.passwordMin'))
  .max(limits.passwordMax, msg('validation.passwordMax'))

export const displayName = z
  .string()
  .trim()
  .min(limits.displayNameMin, msg('validation.displayName'))
  .max(limits.displayNameMax, msg('validation.displayName'))

export const loginSchema = z.object({
  email,
  password: z.string().min(1, msg('validation.required')),
})

export const registerSchema = z.object({ displayName, email, password: newPassword })

export const emailSchema = z.object({ email })

export const resetPasswordSchema = z
  .object({ newPassword, confirmPassword: z.string() })
  .refine((v) => v.newPassword === v.confirmPassword, {
    path: ['confirmPassword'],
    ...msg('validation.passwordsMatch'),
  })

export const changePasswordSchema = z.object({
  currentPassword: z.string().min(1, msg('validation.required')),
  newPassword,
})

/** Params for messages whose text mentions a limit. */
export const messageParams: Partial<Record<MessageKey, Record<string, number>>> = {
  'validation.passwordMin': { min: limits.passwordMin },
  'validation.passwordMax': { max: limits.passwordMax },
  'validation.displayName': { min: limits.displayNameMin, max: limits.displayNameMax },
  'validation.bioMax': { max: limits.bioMax },
  'validation.areaLabel': { max: limits.areaLabelMax },
}
