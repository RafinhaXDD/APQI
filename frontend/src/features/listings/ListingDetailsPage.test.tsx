import { screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from '../../App'
import { setAccessToken } from '../../services/api'
import { json, mockFetch, renderApp } from '../../test/utils'

const listing = (isMine: boolean) => ({
  id: 'l1',
  book: {
    id: 'b1',
    isbn: '9780140328721',
    title: 'Fantastic Mr. Fox',
    authors: ['Roald Dahl'],
    publishedYear: 1988,
    coverUrl: null,
    source: 'OpenLibrary',
  },
  description: null,
  condition: 'Good',
  status: 'Active',
  areaLabel: 'Campus Norte',
  distance: null,
  images: [],
  owner: { id: 'u1', displayName: 'Rafa' },
  isMine,
  myLocation: isMine ? { areaLabel: 'Campus Norte', latitude: -23.5, longitude: -46.7 } : null,
  createdAt: '2026-10-02T12:00:00Z',
})

describe('listing details', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    setAccessToken(null)
  })

  it('waits for the session to be restored before loading, so the owner sees their controls', async () => {
    mockFetch({
      // A slow session restore: the listing must not be fetched signed-out in the meantime.
      'POST /api/auth/refresh': async () => {
        await new Promise((resolve) => setTimeout(resolve, 50))
        return json(200, { accessToken: 'owner-token', expiresAt: '2026-10-02T12:15:00Z' })()
      },
      'GET /api/auth/me': json(200, {
        id: 'u1',
        email: 'rafa@example.test',
        emailConfirmed: true,
        displayName: 'Rafa',
        preferredLanguage: 'pt-BR',
      }),
      'GET /api/listings/l1': (init) => {
        const auth = (init?.headers as Record<string, string>).Authorization
        return json(200, listing(auth === 'Bearer owner-token'))()
      },
    })
    renderApp(<App />, { route: '/listings/l1' })

    expect(await screen.findByRole('link', { name: 'Editar anúncio' })).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Pedir este livro' })).toBeNull()
  })
})
