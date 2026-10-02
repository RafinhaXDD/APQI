/**
 * The single API client (R-25). Components never call fetch directly.
 * - Access token lives in memory only (R-21); the refresh token is an HttpOnly cookie the browser sends.
 * - On 401 it refreshes once (concurrent 401s share one refresh) and retries the request once.
 * - Errors become ApiError with the server's stable `code`, which the UI translates.
 */

export class ApiError extends Error {
  readonly status: number
  readonly code: string
  readonly fieldErrors: Record<string, string[]>

  constructor(
    status: number,
    code: string,
    message: string,
    fieldErrors: Record<string, string[]> = {},
  ) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = code
    this.fieldErrors = fieldErrors
  }
}

type AccessTokenResponse = { accessToken: string; expiresAt: string }

let accessToken: string | null = null
let refreshInFlight: Promise<boolean> | null = null
const sessionExpiredListeners = new Set<() => void>()

export function getAccessToken() {
  return accessToken
}

export function setAccessToken(token: string | null) {
  accessToken = token
}

/** Called when a refresh fails, i.e. the user is no longer signed in. Returns an unsubscribe function. */
export function onSessionExpired(listener: () => void) {
  sessionExpiredListeners.add(listener)
  return () => {
    sessionExpiredListeners.delete(listener)
  }
}

/** Exchanges the refresh cookie for a new access token. Concurrent callers share one request. */
export function refreshSession(): Promise<boolean> {
  refreshInFlight ??= (async () => {
    try {
      const response = await fetch('/api/auth/refresh', {
        method: 'POST',
        credentials: 'same-origin',
      })
      if (!response.ok) {
        accessToken = null
        return false
      }
      accessToken = ((await response.json()) as AccessTokenResponse).accessToken
      return true
    } catch {
      return false
    } finally {
      refreshInFlight = null
    }
  })()
  return refreshInFlight
}

export type RequestOptions = {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
  signal?: AbortSignal
  /** false for login/register-style calls: a 401 there is an answer, not an expired session. */
  retryOnUnauthorized?: boolean
}

export async function apiRequest<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { retryOnUnauthorized = true } = options
  let response = await send(path, options)

  if (response.status === 401 && retryOnUnauthorized) {
    if (await refreshSession()) {
      response = await send(path, options)
    } else {
      sessionExpiredListeners.forEach((listener) => listener())
    }
  }

  if (!response.ok) throw await toApiError(response)
  if (response.status === 204 || response.status === 202) return undefined as T
  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}

/** Login: stores the returned access token in memory. */
export async function startSession(path: string, body: unknown, signal?: AbortSignal) {
  const result = await apiRequest<AccessTokenResponse>(path, {
    method: 'POST',
    body,
    signal,
    retryOnUnauthorized: false,
  })
  accessToken = result.accessToken
}

async function send(path: string, { method = 'GET', body, signal }: RequestOptions) {
  const headers: Record<string, string> = { Accept: 'application/json' }
  // FormData (photo uploads) sets its own multipart Content-Type with the boundary.
  const isForm = body instanceof FormData
  if (body !== undefined && !isForm) headers['Content-Type'] = 'application/json'
  if (accessToken) headers.Authorization = `Bearer ${accessToken}`

  try {
    return await fetch(path, {
      method,
      headers,
      body: body === undefined ? undefined : isForm ? body : JSON.stringify(body),
      signal,
      credentials: 'same-origin',
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw new ApiError(0, 'network_error', 'Network error')
  }
}

async function toApiError(response: Response): Promise<ApiError> {
  try {
    const problem = (await response.json()) as {
      code?: string
      title?: string
      errors?: Record<string, string[]>
    }
    return new ApiError(
      response.status,
      problem.code ?? fallbackCode(response.status),
      problem.title ?? response.statusText,
      problem.errors ?? {},
    )
  } catch {
    return new ApiError(response.status, fallbackCode(response.status), response.statusText)
  }
}

function fallbackCode(status: number) {
  if (status === 401) return 'auth.unauthorized'
  if (status === 404) return 'not_found'
  if (status === 409) return 'conflict'
  if (status === 429) return 'rate_limited'
  if (status === 422 || status === 400) return 'validation.failed'
  return 'server_error'
}
