import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { act, render, renderHook, screen, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ToastProvider } from '../context/ToastContext'
import { useLiveAlerts } from './useLiveAlerts'
import type { AlertMessage } from '../types'

const hub = vi.hoisted(() => ({
  handlers: {} as Record<string, (message: unknown) => void>,
  onClose: undefined as undefined | (() => void),
  start: vi.fn(),
  stop: vi.fn(),
}))

vi.mock('@microsoft/signalr', () => {
  class HubConnectionBuilder {
    withUrl() { return this }
    withAutomaticReconnect() { return this }
    configureLogging() { return this }
    build() {
      return {
        on: (event: string, handler: (message: unknown) => void) => { hub.handlers[event] = handler },
        onreconnecting: vi.fn(),
        onreconnected: vi.fn(),
        onclose: (handler: () => void) => { hub.onClose = handler },
        start: hub.start,
        stop: hub.stop,
      }
    }
  }
  return { HubConnectionBuilder, LogLevel: { Warning: 3 } }
})

let queryClient: QueryClient
const wrapper = ({ children }: { children: ReactNode }) => (
  <QueryClientProvider client={queryClient}>
    <MemoryRouter>
      <ToastProvider>{children}</ToastProvider>
    </MemoryRouter>
  </QueryClientProvider>
)

const message = (overrides: Partial<AlertMessage>): AlertMessage => ({
  type: 'ANALYSIS_COMPLETED', repositoryId: 'r1', repositoryName: 'acme/shop', timestamp: new Date().toISOString(), ...overrides,
})

describe('useLiveAlerts', () => {
  beforeEach(() => {
    queryClient = new QueryClient()
    hub.handlers = {}
    hub.start.mockResolvedValue(undefined)
    hub.stop.mockResolvedValue(undefined)
  })
  afterEach(() => vi.clearAllMocks())

  it('stays offline and does not connect when signed out', () => {
    const { result } = renderHook(() => useLiveAlerts(false), { wrapper })
    expect(result.current).toBe('offline')
    expect(hub.start).not.toHaveBeenCalled()
  })

  it('connects, goes live and stops on unmount', async () => {
    const { result, unmount } = renderHook(() => useLiveAlerts(true), { wrapper })
    await waitFor(() => expect(result.current).toBe('live'))
    act(() => hub.onClose?.())
    expect(result.current).toBe('offline')
    unmount()
    expect(hub.stop).toHaveBeenCalled()
  })

  it('reports offline when the connection cannot start', async () => {
    hub.start.mockRejectedValue(new Error('blocked'))
    const { result } = renderHook(() => useLiveAlerts(true), { wrapper })
    await waitFor(() => expect(hub.start).toHaveBeenCalled())
    await waitFor(() => expect(result.current).toBe('offline'))
  })

  it('refreshes cached data and raises toasts for meaningful events', async () => {
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries')
    function Probe() {
      useLiveAlerts(true)
      return null
    }
    render(<Probe />, { wrapper })
    await waitFor(() => expect(hub.handlers.ReceiveAlert).toBeDefined())

    act(() => hub.handlers.ReceiveAlert(message({ type: 'ANALYSIS_PROGRESS', status: 'Parsing' })))
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['repository', 'r1'] })
    expect(screen.queryByRole('status')).not.toBeInTheDocument()

    act(() => hub.handlers.ReceiveAlert(message({ critical: 1, high: 2 })))
    expect(await screen.findByText('Scan complete · acme/shop')).toBeInTheDocument()
    expect(screen.getByText('1 critical and 2 high severity findings')).toBeInTheDocument()
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['dashboard'] })

    act(() => hub.handlers.ReceiveAlert(message({ type: 'ANALYSIS_FAILED', message: 'Repository not found' })))
    expect(await screen.findByText('Repository not found')).toBeInTheDocument()

    act(() => hub.handlers.ReceiveAlert(message({ type: 'NEW_VULNERABILITY', severity: 'Critical', packageName: 'lodash', cveId: 'CVE-1' })))
    expect(await screen.findByText('New critical vulnerability in lodash')).toBeInTheDocument()

    act(() => hub.handlers.ReceiveAlert(message({ critical: 0, high: 0, repositoryName: 'acme/clean' })))
    expect(await screen.findByText('No critical or high severity findings')).toBeInTheDocument()
  })
})
