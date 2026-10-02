import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from '../../App'
import { setAccessToken } from '../../services/api'
import { bodyOf, json, mockFetch, problem, renderApp } from '../../test/utils'

const tokens = { accessToken: 'access-1', expiresAt: '2026-10-02T12:15:00Z' }
const me = {
  id: 'u1',
  email: 'ana@example.test',
  emailConfirmed: true,
  displayName: 'Ana',
  preferredLanguage: 'pt-BR',
}
const profile = (withHome: boolean) => ({
  userId: 'u1',
  displayName: 'Ana',
  bio: null,
  preferredLanguage: 'pt-BR',
  homeArea: withHome ? { label: 'Centro', latitude: -23.55, longitude: -46.63 } : null,
  credits: { available: 1, held: 0 },
})
const book = {
  id: 'b1',
  isbn: '9788535914849',
  title: 'Ensaio sobre a Cegueira',
  authors: ['José Saramago'],
  publishedYear: 1995,
  coverUrl: null,
  source: 'OpenLibrary',
}
const details = {
  id: 'l1',
  book,
  description: null,
  condition: 'Good',
  status: 'Active',
  areaLabel: 'Centro',
  distance: null,
  images: [],
  owner: { id: 'u1', displayName: 'Ana' },
  isMine: true,
  myLocation: { areaLabel: 'Centro', latitude: -23.55, longitude: -46.63 },
  createdAt: '2026-10-02T12:00:00Z',
}

function signedIn(withHome = true) {
  return {
    'POST /api/auth/refresh': json(200, tokens),
    'GET /api/auth/me': json(200, me),
    'GET /api/users/me': json(200, profile(withHome)),
  }
}

async function typeIsbn(value: string) {
  const user = userEvent.setup()
  await user.type(await screen.findByLabelText('ISBN (código de barras)'), value)
  await user.click(screen.getByRole('button', { name: 'Buscar' }))
  return user
}

describe('create listing (scan → metadata → publish)', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    setAccessToken(null)
  })

  it('fills the book from the ISBN lookup and publishes with its id', async () => {
    const fetchMock = mockFetch({
      ...signedIn(),
      'GET /api/books/lookup?isbn=9788535914849': json(200, book),
      'POST /api/listings': json(201, details),
      'GET /api/listings/l1': json(200, details),
    })
    renderApp(<App />, { route: '/listings/new' })

    const user = await typeIsbn('978-85-359-1484-9')
    expect(await screen.findByText('Ensaio sobre a Cegueira')).toBeTruthy()
    await user.click(screen.getByText('Ficção'))
    await user.click(screen.getByText('Como novo'))
    await user.click(screen.getByRole('button', { name: 'Publicar' }))

    expect(await screen.findByRole('heading', { name: 'Ensaio sobre a Cegueira' })).toBeTruthy()
    expect(bodyOf(fetchMock, 'POST /api/listings')).toEqual({
      bookId: 'b1',
      condition: 'LikeNew',
      category: 'Fiction',
      goodForBeginners: false,
      description: null,
    })
  })

  it('switches to manual entry, keeping the ISBN, when the book is not found', async () => {
    const fetchMock = mockFetch({
      ...signedIn(),
      'GET /api/books/lookup?isbn=9781234567897': problem(404, 'book.not_found'),
      'POST /api/listings': json(201, details),
      'GET /api/listings/l1': json(200, details),
    })
    renderApp(<App />, { route: '/listings/new' })

    const user = await typeIsbn('9781234567897')
    expect(
      await screen.findByText('Não encontramos este livro. Preencha os dados abaixo.'),
    ).toBeTruthy()
    await user.type(screen.getByLabelText('Título'), 'Livro Raro')
    await user.type(
      screen.getByLabelText('Autores (separados por vírgula)'),
      'Autora Um, Autor Dois',
    )
    await user.click(screen.getByText('Clássicos da literatura'))
    await user.click(screen.getByLabelText(/Bom para começar/))
    await user.click(screen.getByRole('button', { name: 'Publicar' }))

    await screen.findByRole('heading', { name: 'Ensaio sobre a Cegueira' })
    expect(bodyOf(fetchMock, 'POST /api/listings')).toEqual({
      manualBook: {
        isbn: '9781234567897',
        title: 'Livro Raro',
        authors: ['Autora Um', 'Autor Dois'],
      },
      condition: 'Good',
      category: 'Classics',
      goodForBeginners: true,
      description: null,
    })
  })

  it('requires a category before publishing', async () => {
    const fetchMock = mockFetch({
      ...signedIn(),
      'GET /api/books/lookup?isbn=9788535914849': json(200, book),
    })
    renderApp(<App />, { route: '/listings/new' })

    const user = await typeIsbn('9788535914849')
    await user.click(await screen.findByRole('button', { name: 'Publicar' }))

    expect(await screen.findByText('Escolha uma categoria.')).toBeTruthy()
    expect(
      fetchMock.mock.calls.some(
        ([url, init]) => init?.method === 'POST' && String(url) === '/api/listings',
      ),
    ).toBe(false)
  })

  it('rejects an invalid ISBN without calling the API', async () => {
    const fetchMock = mockFetch(signedIn())
    renderApp(<App />, { route: '/listings/new' })

    await typeIsbn('9788535914848')

    expect(await screen.findByText('Este ISBN não é válido. Confira os números.')).toBeTruthy()
    expect(fetchMock.mock.calls.some(([url]) => String(url).startsWith('/api/books'))).toBe(false)
  })

  it('asks for a home area before publishing', async () => {
    mockFetch({ ...signedIn(false), 'GET /api/books/lookup?isbn=9788535914849': json(200, book) })
    renderApp(<App />, { route: '/listings/new' })

    expect(await screen.findByText('Antes de anunciar, defina sua região no perfil.')).toBeTruthy()
    await typeIsbn('9788535914849')

    const publish = await screen.findByRole('button', { name: 'Publicar' })
    expect((publish as HTMLButtonElement).disabled).toBe(true)
  })
})
