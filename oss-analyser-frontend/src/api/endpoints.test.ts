import { afterEach, describe, expect, it, vi } from 'vitest'
import { authApi, dashboardApi, repositoriesApi } from './endpoints'

const calls = () => {
  const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async () => new Response('{}', { status: 200 }))
  return () => fetchMock.mock.calls.map(([url, init]) => `${init?.method} ${url}`)
}

describe('endpoints', () => {
  afterEach(() => vi.restoreAllMocks())

  it('maps every call to the REST API', async () => {
    const recorded = calls()
    await authApi.me()
    await authApi.login('a@b.c', 'pw')
    await authApi.register('A', 'a@b.c', 'pw')
    await authApi.logout()
    await dashboardApi.get()
    await repositoriesApi.list()
    await repositoriesApi.list({ search: 'acme shop', status: 'Failed' })
    await repositoriesApi.get('r1')
    await repositoriesApi.create('acme/shop')
    await repositoriesApi.rescan('r1')
    await repositoriesApi.remove('r1')
    await repositoriesApi.dependencies('r1')
    await repositoriesApi.vulnerabilities('r1')
    await repositoriesApi.snapshots('r1')

    expect(recorded()).toEqual([
      'GET /api/auth/me',
      'POST /api/auth/login',
      'POST /api/auth/register',
      'POST /api/auth/logout',
      'GET /api/dashboard',
      'GET /api/repositories',
      'GET /api/repositories?search=acme+shop&status=Failed',
      'GET /api/repositories/r1',
      'POST /api/repositories',
      'POST /api/repositories/r1/rescan',
      'DELETE /api/repositories/r1',
      'GET /api/repositories/r1/dependencies',
      'GET /api/repositories/r1/vulnerabilities',
      'GET /api/repositories/r1/snapshots',
    ])
    expect(repositoriesApi.sbomUrl('r1')).toBe('/api/repositories/r1/sbom')
  })
})
