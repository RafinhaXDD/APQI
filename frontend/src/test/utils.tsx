import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import type { ReactNode } from 'react'
import { MemoryRouter } from 'react-router'
import { vi } from 'vitest'
import { AuthProvider } from '../features/auth/AuthProvider'
import { SearchOriginProvider } from '../features/listings/origin'
import type { Language } from '../i18n/i18n'
import { I18nProvider } from '../i18n/I18nProvider'

type Handler = (init: RequestInit | undefined) => Response | Promise<Response>

/**
 * Stubs global fetch with a route table keyed by "METHOD /path". Unknown routes fail the test loudly.
 * Returns the mock so tests can inspect calls.
 */
export function mockFetch(routes: Record<string, Handler | Handler[]>) {
  const queues = new Map(
    Object.entries(routes).map(([key, value]) => [key, Array.isArray(value) ? [...value] : value]),
  )
  const mock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const key = `${init?.method ?? 'GET'} ${String(input)}`
    const route = queues.get(key)
    if (!route) throw new Error(`Unexpected request: ${key}`)
    const handler = Array.isArray(route) ? (route.length > 1 ? route.shift()! : route[0]) : route
    return handler(init)
  })
  vi.stubGlobal('fetch', mock)
  return mock
}

export const json = (status: number, body?: unknown) => () =>
  new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: { 'Content-Type': status >= 400 ? 'application/problem+json' : 'application/json' },
  })

export const problem = (status: number, code: string) => json(status, { status, code, title: code })

export function bodyOf(mock: ReturnType<typeof mockFetch>, key: string) {
  const call = mock.mock.calls.find(
    ([input, init]) => `${init?.method ?? 'GET'} ${String(input)}` === key,
  )
  return call ? JSON.parse(String(call[1]?.body)) : undefined
}

/** Real providers (i18n, query, router, auth) around the given UI. */
export function renderApp(ui: ReactNode, { route = '/', language = 'pt-BR' as Language } = {}) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return render(
    <I18nProvider initialLanguage={language}>
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={[route]}>
          <AuthProvider>
            <SearchOriginProvider>{ui}</SearchOriginProvider>
          </AuthProvider>
        </MemoryRouter>
      </QueryClientProvider>
    </I18nProvider>,
  )
}
