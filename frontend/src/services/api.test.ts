import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { json, mockFetch, problem } from '../test/utils'
import { ApiError, apiRequest, getAccessToken, onSessionExpired, setAccessToken } from './api'

const refreshOk = (token: string) =>
  json(200, { accessToken: token, expiresAt: '2026-10-02T12:15:00Z' })

function authHeader(mock: ReturnType<typeof mockFetch>, index: number) {
  return (mock.mock.calls[index][1]?.headers as Record<string, string>).Authorization
}

describe('apiRequest (R-25)', () => {
  beforeEach(() => setAccessToken(null))
  afterEach(() => vi.unstubAllGlobals())

  it('attaches the in-memory access token as a bearer header', async () => {
    setAccessToken('token-1')
    const mock = mockFetch({ 'GET /api/users/me': json(200, { ok: true }) })

    await expect(apiRequest('/api/users/me')).resolves.toEqual({ ok: true })
    expect(authHeader(mock, 0)).toBe('Bearer token-1')
  })

  it('on 401 refreshes once and retries with the new token', async () => {
    setAccessToken('expired')
    const mock = mockFetch({
      'GET /api/users/me': [problem(401, 'auth.unauthorized'), json(200, { ok: true })],
      'POST /api/auth/refresh': refreshOk('fresh'),
    })

    await expect(apiRequest('/api/users/me')).resolves.toEqual({ ok: true })
    expect(mock).toHaveBeenCalledTimes(3)
    expect(authHeader(mock, 2)).toBe('Bearer fresh')
    expect(getAccessToken()).toBe('fresh')
  })

  it('shares one refresh between concurrent 401s', async () => {
    setAccessToken('expired')
    let refreshCalls = 0
    mockFetch({
      'GET /api/a': [problem(401, 'auth.unauthorized'), json(200, 'a')],
      'GET /api/b': [problem(401, 'auth.unauthorized'), json(200, 'b')],
      'POST /api/auth/refresh': async () => {
        refreshCalls++
        await new Promise((resolve) => setTimeout(resolve, 10))
        return refreshOk('fresh')()
      },
    })

    await expect(Promise.all([apiRequest('/api/a'), apiRequest('/api/b')])).resolves.toEqual([
      'a',
      'b',
    ])
    expect(refreshCalls).toBe(1)
  })

  it('reports an expired session when the refresh fails, without looping', async () => {
    setAccessToken('expired')
    const expired = vi.fn()
    const unsubscribe = onSessionExpired(expired)
    const mock = mockFetch({
      'GET /api/users/me': problem(401, 'auth.unauthorized'),
      'POST /api/auth/refresh': problem(401, 'auth.session_expired'),
    })

    await expect(apiRequest('/api/users/me')).rejects.toMatchObject({
      status: 401,
      code: 'auth.unauthorized',
    })
    expect(expired).toHaveBeenCalledOnce()
    expect(mock).toHaveBeenCalledTimes(2)
    expect(getAccessToken()).toBeNull()
    unsubscribe()
  })

  it('does not refresh when the caller opts out (login-style calls)', async () => {
    const mock = mockFetch({ 'POST /api/auth/login': problem(401, 'auth.invalid_credentials') })

    await expect(
      apiRequest('/api/auth/login', { method: 'POST', body: {}, retryOnUnauthorized: false }),
    ).rejects.toMatchObject({ code: 'auth.invalid_credentials' })
    expect(mock).toHaveBeenCalledOnce()
  })

  it('normalizes ProblemDetails into ApiError with code and field errors', async () => {
    mockFetch({
      'PUT /api/users/me': json(422, {
        status: 422,
        title: 'One or more fields are invalid.',
        code: 'validation.failed',
        errors: { displayName: ['Too short.'] },
      }),
    })

    const error = await apiRequest('/api/users/me', { method: 'PUT', body: {} }).catch(
      (e: unknown) => e,
    )

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({
      status: 422,
      code: 'validation.failed',
      fieldErrors: { displayName: ['Too short.'] },
    })
  })

  it('turns a network failure into a network_error ApiError', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')))

    await expect(apiRequest('/api/users/me')).rejects.toMatchObject({
      status: 0,
      code: 'network_error',
    })
  })

  it('passes the AbortSignal through and lets aborts propagate', async () => {
    const controller = new AbortController()
    const mock = vi.fn((_input: RequestInfo | URL, init?: RequestInit) => {
      expect(init?.signal).toBe(controller.signal)
      return Promise.reject(new DOMException('Aborted', 'AbortError'))
    })
    vi.stubGlobal('fetch', mock)
    controller.abort()

    await expect(apiRequest('/api/users/me', { signal: controller.signal })).rejects.toMatchObject({
      name: 'AbortError',
    })
  })

  it('returns undefined for 204 and 202 responses', async () => {
    mockFetch({
      'POST /api/auth/logout': () => new Response(null, { status: 204 }),
      'POST /api/auth/register': () => new Response(null, { status: 202 }),
    })

    await expect(apiRequest('/api/auth/logout', { method: 'POST' })).resolves.toBeUndefined()
    await expect(
      apiRequest('/api/auth/register', { method: 'POST', body: {} }),
    ).resolves.toBeUndefined()
  })
})
