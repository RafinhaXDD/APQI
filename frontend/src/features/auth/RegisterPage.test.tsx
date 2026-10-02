import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from '../../App'
import { bodyOf, mockFetch, problem, renderApp } from '../../test/utils'

describe('registration', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('sends the current UI language and asks the user to check their email', async () => {
    const fetchMock = mockFetch({
      'POST /api/auth/refresh': problem(401, 'auth.session_expired'),
      'POST /api/auth/register': () => new Response(null, { status: 202 }),
    })
    renderApp(<App />, { route: '/register', language: 'en' })
    const user = userEvent.setup()

    await user.type(await screen.findByLabelText('Display name'), 'Ben')
    await user.type(screen.getByLabelText('Email'), 'ben@example.test')
    await user.type(screen.getByLabelText('Password'), 'correct horse battery')
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    expect(await screen.findByRole('heading', { name: 'Check your email' })).toBeTruthy()
    expect(screen.getByText(/ben@example.test/)).toBeTruthy()
    expect(bodyOf(fetchMock, 'POST /api/auth/register')).toEqual({
      displayName: 'Ben',
      email: 'ben@example.test',
      password: 'correct horse battery',
      preferredLanguage: 'en',
    })
  })

  it('mirrors the server password rule on the client', async () => {
    mockFetch({ 'POST /api/auth/refresh': problem(401, 'auth.session_expired') })
    renderApp(<App />, { route: '/register' })
    const user = userEvent.setup()

    await user.type(await screen.findByLabelText('Senha'), 'short')
    await user.click(screen.getByRole('button', { name: 'Criar conta' }))

    expect(await screen.findByText('A senha precisa ter pelo menos 10 caracteres.')).toBeTruthy()
  })

  it('switching the language translates the page and sets <html lang>', async () => {
    mockFetch({ 'POST /api/auth/refresh': problem(401, 'auth.session_expired') })
    renderApp(<App />, { route: '/register' })

    await userEvent.setup().selectOptions(await screen.findByLabelText('Idioma'), 'en')

    expect(await screen.findByRole('heading', { name: 'Sign up' })).toBeTruthy()
    expect(document.documentElement.lang).toBe('en')
  })
})
