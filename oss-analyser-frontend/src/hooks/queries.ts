import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { dashboardApi, repositoriesApi, type RepositoryFilters } from '../api/endpoints'
import { isInProgress } from '../lib/severity'
import type { Repository } from '../types'

export const queryKeys = {
  dashboard: ['dashboard'] as const,
  repositories: (filters: RepositoryFilters = {}) => ['repositories', filters] as const,
  repository: (id: string) => ['repository', id] as const,
  dependencies: (id: string) => ['repository', id, 'dependencies'] as const,
  vulnerabilities: (id: string) => ['repository', id, 'vulnerabilities'] as const,
  snapshots: (id: string) => ['repository', id, 'snapshots'] as const,
}

/**
 * Live updates arrive over SignalR; polling while a scan is running is the fallback for
 * proxies that block WebSockets, so progress is never stuck.
 */
const POLL_WHILE_SCANNING_MS = 4000

export const useDashboard = () =>
  useQuery({ queryKey: queryKeys.dashboard, queryFn: ({ signal }) => dashboardApi.get(signal) })

export const useRepositories = (filters: RepositoryFilters = {}) =>
  useQuery({
    queryKey: queryKeys.repositories(filters),
    queryFn: ({ signal }) => repositoriesApi.list(filters, signal),
    placeholderData: keepPreviousData,
    refetchInterval: (query) => (query.state.data?.some((r) => isInProgress(r.status)) ? POLL_WHILE_SCANNING_MS : false),
  })

export const useRepository = (id: string) =>
  useQuery({
    queryKey: queryKeys.repository(id),
    queryFn: ({ signal }) => repositoriesApi.get(id, signal),
    refetchInterval: (query) => (query.state.data && isInProgress(query.state.data.status) ? POLL_WHILE_SCANNING_MS : false),
  })

export const useDependencies = (id: string, enabled: boolean) =>
  useQuery({ queryKey: queryKeys.dependencies(id), queryFn: ({ signal }) => repositoriesApi.dependencies(id, signal), enabled })

export const useVulnerabilities = (id: string, enabled: boolean) =>
  useQuery({ queryKey: queryKeys.vulnerabilities(id), queryFn: ({ signal }) => repositoriesApi.vulnerabilities(id, signal), enabled })

export const useSnapshots = (id: string, enabled: boolean) =>
  useQuery({ queryKey: queryKeys.snapshots(id), queryFn: ({ signal }) => repositoriesApi.snapshots(id, signal), enabled })

const useInvalidateRepositoryData = () => {
  const client = useQueryClient()
  return (repository?: Repository) => {
    void client.invalidateQueries({ queryKey: ['repositories'] })
    void client.invalidateQueries({ queryKey: queryKeys.dashboard })
    if (repository) client.setQueryData(queryKeys.repository(repository.id), repository)
  }
}

export const useCreateScan = () => {
  const invalidate = useInvalidateRepositoryData()
  return useMutation({ mutationFn: (repositoryUrl: string) => repositoriesApi.create(repositoryUrl), onSuccess: invalidate })
}

export const useRescan = () => {
  const invalidate = useInvalidateRepositoryData()
  return useMutation({ mutationFn: (id: string) => repositoriesApi.rescan(id), onSuccess: invalidate })
}

export const useDeleteRepository = () => {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => repositoriesApi.remove(id),
    onSuccess: (_, id) => {
      client.removeQueries({ queryKey: queryKeys.repository(id) })
      void client.invalidateQueries({ queryKey: ['repositories'] })
      void client.invalidateQueries({ queryKey: queryKeys.dashboard })
    },
  })
}
