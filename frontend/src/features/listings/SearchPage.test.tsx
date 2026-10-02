import { screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from '../../App'
import { setAccessToken } from '../../services/api'
import { json, mockFetch, problem, renderApp } from '../../test/utils'

const summary = (id: string, title: string, distanceKm: number, upper: boolean) => ({
  id,
  title,
  authors: ['Machado de Assis'],
  coverUrl: null,
  thumbnailUrl: null,
  condition: 'Good',
  areaLabel: 'Centro',
  distanceKm,
  distanceIsUpperBound: upper,
  ownerDisplayName: 'Bruno',
  isMine: false,
  createdAt: '2026-10-02T12:00:00Z',
})

describe('search near me', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    setAccessToken(null)
  })

  it('searches around the home area and shows rounded, translated distances', async () => {
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
        credits: { available: 1, held: 0 },
      }),
      'GET /api/listings?radiusKm=5&sort=Distance&page=1&pageSize=20': json(200, {
        items: [summary('l1', 'Dom Casmurro', 1, true), summary('l2', 'Quincas Borba', 2.5, false)],
        page: 1,
        pageSize: 20,
        totalCount: 2,
      }),
    })
    renderApp(<App />, { route: '/search' })

    expect(await screen.findByText('Dom Casmurro')).toBeTruthy()
    expect(screen.getByText('2 livros encontrados')).toBeTruthy()
    expect(screen.getByText('Perto da sua região: Centro')).toBeTruthy()
    expect(screen.getByText(/a menos de 1 km/)).toBeTruthy()
    expect(screen.getByText(/a 2,5 km/)).toBeTruthy()
    // The home area is resolved by the server: no coordinates go into the URL.
    expect(fetchMock.mock.calls.some(([url]) => String(url).includes('lat='))).toBe(false)
  })

  it('asks anonymous visitors for a location instead of searching', async () => {
    const fetchMock = mockFetch({ 'POST /api/auth/refresh': problem(401, 'auth.session_expired') })
    renderApp(<App />, { route: '/search' })

    expect(
      await screen.findByText(
        'Para ver livros perto de você, compartilhe sua localização ou entre e defina sua região.',
      ),
    ).toBeTruthy()
    expect(fetchMock.mock.calls.some(([url]) => String(url).startsWith('/api/listings'))).toBe(
      false,
    )
  })
})
