import { api } from '../lib/api'
import type { AnalysisStatus, Dashboard, Dependency, Repository, Snapshot, User, Vulnerability } from '../types'

export const authApi = {
  me: (signal?: AbortSignal) => api.get<User>('/api/auth/me', signal),
  login: (email: string, password: string) => api.post<User>('/api/auth/login', { email, password }),
  register: (displayName: string, email: string, password: string) =>
    api.post<User>('/api/auth/register', { displayName, email, password }),
  logout: () => api.post<void>('/api/auth/logout'),
}

export interface RepositoryFilters {
  search?: string
  status?: AnalysisStatus
}

export const repositoriesApi = {
  list: (filters: RepositoryFilters = {}, signal?: AbortSignal) => {
    const params = new URLSearchParams()
    if (filters.search) params.set('search', filters.search)
    if (filters.status) params.set('status', filters.status)
    const query = params.toString()
    return api.get<Repository[]>(`/api/repositories${query ? `?${query}` : ''}`, signal)
  },
  get: (id: string, signal?: AbortSignal) => api.get<Repository>(`/api/repositories/${id}`, signal),
  create: (repositoryUrl: string) => api.post<Repository>('/api/repositories', { repositoryUrl }),
  rescan: (id: string) => api.post<Repository>(`/api/repositories/${id}/rescan`),
  remove: (id: string) => api.delete(`/api/repositories/${id}`),
  dependencies: (id: string, signal?: AbortSignal) => api.get<Dependency[]>(`/api/repositories/${id}/dependencies`, signal),
  vulnerabilities: (id: string, signal?: AbortSignal) => api.get<Vulnerability[]>(`/api/repositories/${id}/vulnerabilities`, signal),
  snapshots: (id: string, signal?: AbortSignal) => api.get<Snapshot[]>(`/api/repositories/${id}/snapshots`, signal),
  sbomUrl: (id: string) => `/api/repositories/${id}/sbom`,
}

export const dashboardApi = {
  get: (signal?: AbortSignal) => api.get<Dashboard>('/api/dashboard', signal),
}
