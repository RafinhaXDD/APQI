import type { Language } from '../i18n/i18n'
import { apiRequest, setAccessToken, startSession } from './api'

export type Me = {
  id: string
  email: string
  emailConfirmed: boolean
  displayName: string
  preferredLanguage: Language
}

export type HomeArea = { label: string; latitude: number; longitude: number }

export type MyProfile = {
  userId: string
  displayName: string
  bio: string | null
  preferredLanguage: Language
  homeArea: HomeArea | null
  credits: { available: number; held: number }
}

export const authApi = {
  register: (body: {
    email: string
    password: string
    displayName: string
    preferredLanguage: Language
  }) =>
    apiRequest<void>('/api/auth/register', { method: 'POST', body, retryOnUnauthorized: false }),
  login: (body: { email: string; password: string }) => startSession('/api/auth/login', body),
  logout: async () => {
    try {
      await apiRequest<void>('/api/auth/logout', { method: 'POST', retryOnUnauthorized: false })
    } finally {
      setAccessToken(null)
    }
  },
  me: (signal?: AbortSignal) => apiRequest<Me>('/api/auth/me', { signal }),
  confirmEmail: (body: { userId: string; token: string }) =>
    apiRequest<void>('/api/auth/confirm-email', {
      method: 'POST',
      body,
      retryOnUnauthorized: false,
    }),
  resendConfirmation: (email: string) =>
    apiRequest<void>('/api/auth/resend-confirmation', {
      method: 'POST',
      body: { email },
      retryOnUnauthorized: false,
    }),
  forgotPassword: (email: string) =>
    apiRequest<void>('/api/auth/forgot-password', {
      method: 'POST',
      body: { email },
      retryOnUnauthorized: false,
    }),
  resetPassword: (body: { userId: string; token: string; newPassword: string }) =>
    apiRequest<void>('/api/auth/reset-password', {
      method: 'POST',
      body,
      retryOnUnauthorized: false,
    }),
  changePassword: (body: { currentPassword: string; newPassword: string }) =>
    apiRequest<void>('/api/auth/change-password', { method: 'POST', body }),
}

export const profileApi = {
  get: (signal?: AbortSignal) => apiRequest<MyProfile>('/api/users/me', { signal }),
  update: (body: { displayName: string; bio: string | null; preferredLanguage: Language }) =>
    apiRequest<MyProfile>('/api/users/me', { method: 'PUT', body }),
  setHomeArea: (body: HomeArea) =>
    apiRequest<MyProfile>('/api/users/me/home-area', { method: 'PUT', body }),
}

/** Query-key factory (R-26): one place per feature, used for both queries and invalidation. */
export const queryKeys = {
  me: ['auth', 'me'] as const,
  profile: ['profile', 'me'] as const,
}
