import type { ReactElement, ReactNode } from 'react'
import { render } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ToastProvider } from '../context/ToastContext'
import { AuthProvider } from '../context/AuthContext'

interface Options {
  route?: string
  path?: string
  withAuth?: boolean
  extraRoutes?: ReactNode
}

/** Renders inside the same providers as the app, with a fresh non-retrying query cache. */
export function renderWithProviders(ui: ReactElement, { route = '/', path, withAuth = false, extraRoutes }: Options = {}) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: Infinity }, mutations: { retry: false } } })
  const content = path ? (
    <Routes>
      <Route path={path} element={ui} />
      {extraRoutes}
      <Route path="*" element={<p>Navigated away</p>} />
    </Routes>
  ) : (
    ui
  )
  return {
    queryClient,
    ...render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={[route]}>
          <ToastProvider>{withAuth ? <AuthProvider>{content}</AuthProvider> : content}</ToastProvider>
        </MemoryRouter>
      </QueryClientProvider>,
    ),
  }
}
