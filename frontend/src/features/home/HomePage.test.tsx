import { screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from '../../App'
import { json, mockFetch, problem, renderApp } from '../../test/utils'

describe('landing page', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('explains the cycle and links each category to a pre-filtered search', async () => {
    mockFetch({ 'POST /api/auth/refresh': problem(401, 'auth.session_expired') })
    renderApp(<App />, { route: '/' })

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Troque. Leia. Repita.' }),
    ).toBeTruthy()
    for (const step of ['Anuncie um livro', 'Ganhe uma ficha', 'Escolha outro', 'Leia e repita']) {
      expect(screen.getByRole('heading', { name: step })).toBeTruthy()
    }
    expect(screen.getByRole('link', { name: 'Filosofia e reflexão' }).getAttribute('href')).toBe(
      '/search?category=Philosophy',
    )
    expect(screen.getByRole('link', { name: 'Ver livros para começar' }).getAttribute('href')).toBe(
      '/search?beginners=true',
    )
    // Signed out: listing a book starts with an account.
    expect(screen.getByRole('link', { name: 'Anunciar um livro' }).getAttribute('href')).toBe(
      '/register',
    )
  })

  it('opens the search already filtered when coming from a category link', async () => {
    const fetchMock = mockFetch({
      'POST /api/auth/refresh': json(200, { accessToken: 'a', expiresAt: '2026-10-02T12:15:00Z' }),
      'GET /api/auth/me': json(200, {
        id: 'u1',
        email: 'ana@example.test',
        emailConfirmed: true,
        displayName: 'Ana',
        preferredLanguage: 'pt-BR',
      }),
      'GET /api/users/me': json(200, {
        userId: 'u1',
        displayName: 'Ana',
        bio: null,
        preferredLanguage: 'pt-BR',
        homeArea: { label: 'Centro', latitude: -23.55, longitude: -46.63 },
        credits: { available: 0, held: 0 },
      }),
      'GET /api/listings?radiusKm=5&category=Philosophy&sort=Distance&page=1&pageSize=20': json(
        200,
        {
          items: [],
          page: 1,
          pageSize: 20,
          totalCount: 0,
        },
      ),
    })
    renderApp(<App />, { route: '/search?category=Philosophy' })

    expect(await screen.findByText('0 livros encontrados')).toBeTruthy()
    expect(fetchMock.mock.calls.some(([url]) => String(url).includes('category=Philosophy'))).toBe(
      true,
    )
  })
})
