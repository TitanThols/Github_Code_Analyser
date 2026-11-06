import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError, api } from './api'

const respond = (status: number, body?: unknown, headers: Record<string, string> = {}) =>
  vi.spyOn(globalThis, 'fetch').mockResolvedValue(
    new Response(body === undefined ? null : JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json', ...headers } }),
  )

describe('api client', () => {
  afterEach(() => vi.restoreAllMocks())

  it('sends credentials and JSON, and parses the response', async () => {
    const fetchMock = respond(200, { ok: true })
    await expect(api.post('/api/x', { a: 1 })).resolves.toEqual({ ok: true })
    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/x')
    expect(init).toMatchObject({ method: 'POST', credentials: 'include', body: '{"a":1}' })
    expect((init!.headers as Record<string, string>)['Content-Type']).toBe('application/json')
  })

  it('returns undefined for 204 responses', async () => {
    respond(204)
    await expect(api.delete('/api/x')).resolves.toBeUndefined()
  })

  it('surfaces validation messages from problem details', async () => {
    respond(400, { title: 'One or more validation errors occurred.', errors: { RepositoryUrl: ['Enter a public GitHub repository.'] } })
    const error = await api.post('/api/repositories', {}).catch((e: unknown) => e)
    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).message).toBe('Enter a public GitHub repository.')
    expect((error as ApiError).fieldError('repositoryUrl')).toBe('Enter a public GitHub repository.')
    expect((error as ApiError).fieldError('other')).toBeUndefined()
  })

  it('uses the problem title when there are no field errors', async () => {
    respond(409, { title: 'An analysis is already running for this repository.' })
    await expect(api.post('/api/x')).rejects.toThrow('An analysis is already running')
  })

  it.each([
    [401, 'Your session has expired'],
    [404, 'Not found'],
    [429, 'Too many requests'],
    [503, 'Something went wrong on our side'],
    [418, 'Request failed (418)'],
  ])('falls back to a friendly message for %i without a body', async (status, message) => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response('not json', { status }))
    await expect(api.get('/api/x')).rejects.toThrow(message)
  })

  it('maps network failures to status 0', async () => {
    vi.spyOn(globalThis, 'fetch').mockRejectedValue(new TypeError('Failed to fetch'))
    const error = (await api.get('/api/x').catch((e: unknown) => e)) as ApiError
    expect(error.status).toBe(0)
    expect(error.message).toMatch(/Unable to reach the server/)
  })

  it('rethrows aborts untouched', async () => {
    vi.spyOn(globalThis, 'fetch').mockRejectedValue(new DOMException('aborted', 'AbortError'))
    await expect(api.get('/api/x')).rejects.toBeInstanceOf(DOMException)
  })
})
