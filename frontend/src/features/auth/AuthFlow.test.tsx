import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from '../../App'
import { setAccessToken } from '../../services/api'
import { bodyOf, json, mockFetch, problem, renderApp } from '../../test/utils'

const profile = {
  userId: 'u1',
  displayName: 'Ana',
  bio: null,
  preferredLanguage: 'pt-BR',
  homeArea: null,
  credits: { available: 1, held: 0 },
}
const me = {
  id: 'u1',
  email: 'ana@example.test',
  emailConfirmed: true,
  displayName: 'Ana',
  preferredLanguage: 'pt-BR',
}
const tokens = { accessToken: 'access-1', expiresAt: '2026-10-02T12:15:00Z' }

async function fillLogin(email: string, password: string) {
  const user = userEvent.setup()
  await user.type(await screen.findByLabelText('E-mail'), email)
  await user.type(screen.getByLabelText('Senha'), password)
  await user.click(screen.getByRole('button', { name: 'Entrar' }))
}

describe('auth flow', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    setAccessToken(null)
  })

  it('logs in, lands on the profile and shows the starter credit', async () => {
    const fetchMock = mockFetch({
      'POST /api/auth/refresh': problem(401, 'auth.session_expired'),
      'POST /api/auth/login': json(200, tokens),
      'GET /api/auth/me': json(200, me),
      'GET /api/users/me': json(200, profile),
    })
    renderApp(<App />, { route: '/login' })

    await fillLogin('ana@example.test', 'correct horse battery')

    expect(await screen.findByRole('heading', { name: 'Meu perfil' })).toBeTruthy()
    expect(screen.getByText('1 ficha disponível')).toBeTruthy()
    expect(bodyOf(fetchMock, 'POST /api/auth/login')).toEqual({
      email: 'ana@example.test',
      password: 'correct horse battery',
    })
  })

  it('shows the translated error for wrong credentials', async () => {
    mockFetch({
      'POST /api/auth/refresh': problem(401, 'auth.session_expired'),
      'POST /api/auth/login': problem(401, 'auth.invalid_credentials'),
    })
    renderApp(<App />, { route: '/login' })

    await fillLogin('ana@example.test', 'wrong')

    expect((await screen.findByRole('alert')).textContent).toBe('E-mail ou senha incorretos.')
  })

  it('offers to resend the confirmation email when the email is not confirmed', async () => {
    const fetchMock = mockFetch({
      'POST /api/auth/refresh': problem(401, 'auth.session_expired'),
      'POST /api/auth/login': problem(403, 'auth.email_not_confirmed'),
      'POST /api/auth/resend-confirmation': () => new Response(null, { status: 202 }),
    })
    renderApp(<App />, { route: '/login' })

    await fillLogin('ana@example.test', 'correct horse battery')
    await userEvent
      .setup()
      .click(await screen.findByRole('button', { name: 'Reenviar e-mail de confirmação' }))

    expect(await screen.findByText(/enviamos um novo e-mail/)).toBeTruthy()
    expect(bodyOf(fetchMock, 'POST /api/auth/resend-confirmation')).toEqual({
      email: 'ana@example.test',
    })
  })

  it('validates on the client before calling the API', async () => {
    const fetchMock = mockFetch({ 'POST /api/auth/refresh': problem(401, 'auth.session_expired') })
    renderApp(<App />, { route: '/login' })

    await userEvent.setup().click(await screen.findByRole('button', { name: 'Entrar' }))

    expect(await screen.findAllByText('Preencha este campo.')).toHaveLength(2)
    expect(fetchMock.mock.calls.map(([url]) => String(url))).toEqual(['/api/auth/refresh'])
  })

  it('restores the session from the refresh cookie and redirects anonymous users away from /profile', async () => {
    mockFetch({ 'POST /api/auth/refresh': problem(401, 'auth.session_expired') })
    renderApp(<App />, { route: '/profile' })

    expect(await screen.findByRole('heading', { name: 'Entrar' })).toBeTruthy()
  })

  it('keeps a returning user signed in and logs out', async () => {
    const fetchMock = mockFetch({
      'POST /api/auth/refresh': json(200, tokens),
      'GET /api/auth/me': json(200, me),
      'GET /api/users/me': json(200, profile),
      'POST /api/auth/logout': () => new Response(null, { status: 204 }),
    })
    renderApp(<App />, { route: '/profile' })

    expect(await screen.findByRole('heading', { name: 'Meu perfil' })).toBeTruthy()
    const nav = screen.getByRole('navigation', { name: 'Navegação principal' })
    await userEvent.setup().click(within(nav).getByRole('button', { name: 'Sair' }))

    await waitFor(() => expect(within(nav).getByRole('link', { name: 'Entrar' })).toBeTruthy())
    expect(fetchMock.mock.calls.some(([url]) => String(url) === '/api/auth/logout')).toBe(true)
  })
})
