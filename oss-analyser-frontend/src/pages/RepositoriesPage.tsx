import { useState } from 'react'
import { Link } from 'react-router-dom'
import { FolderGit2, Search, Star } from 'lucide-react'
import { useRepositories } from '../hooks/queries'
import { useDebouncedValue } from '../hooks/useDebouncedValue'
import { PageHeader } from '../components/layout/PageHeader'
import { Card } from '../components/ui/Card'
import { buttonClasses } from '../components/ui/buttonStyles'
import { EmptyState, ErrorState, Skeleton } from '../components/ui/Feedback'
import { RiskGradeBadge, StatusBadge } from '../components/ui/Badges'
import { SeverityBar } from '../components/charts/SeverityBar'
import { formatCompact, relativeTime } from '../lib/format'
import { cn } from '../lib/cn'
import type { AnalysisStatus } from '../types'

const statusFilters: { value: AnalysisStatus | undefined; label: string }[] = [
  { value: undefined, label: 'All' },
  { value: 'Completed', label: 'Completed' },
  { value: 'Failed', label: 'Failed' },
]

export function RepositoriesPage() {
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState<AnalysisStatus | undefined>()
  const debouncedSearch = useDebouncedValue(search)
  const { data, isPending, isError, error, refetch, isFetching } = useRepositories({ search: debouncedSearch || undefined, status })

  return (
    <div>
      <PageHeader
        title="Repositories"
        description="Everything you've scanned, re-checked daily for new advisories."
      />

      <div className="mb-4 flex flex-col gap-3 sm:flex-row sm:items-center">
        <div className="relative flex-1">
          <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-fg-subtle" aria-hidden />
          <input
            type="search"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Filter by owner or name"
            aria-label="Filter repositories"
            className="h-10 w-full rounded-lg border border-border bg-surface pl-9 pr-3 text-sm text-fg outline-none placeholder:text-fg-subtle focus:border-accent"
          />
        </div>
        <div className="inline-flex rounded-lg bg-surface-2 p-1" role="group" aria-label="Filter by status">
          {statusFilters.map((f) => (
            <button
              key={f.label}
              type="button"
              aria-pressed={status === f.value}
              onClick={() => setStatus(f.value)}
              className={cn('h-8 rounded-md px-3 text-sm font-medium transition-colors', status === f.value ? 'bg-surface text-fg shadow-card' : 'text-fg-muted hover:text-fg')}
            >
              {f.label}
            </button>
          ))}
        </div>
      </div>

      {isPending ? (
        <div className="space-y-3">{Array.from({ length: 4 }, (_, i) => <Skeleton key={i} className="h-20 rounded-2xl" />)}</div>
      ) : isError ? (
        <ErrorState message={error.message} onRetry={() => void refetch()} />
      ) : data.length === 0 ? (
        <Card>
          <EmptyState
            icon={<FolderGit2 className="size-6" />}
            title={search || status ? 'No repositories match' : 'No repositories yet'}
            description={search || status ? 'Try a different filter.' : 'Scan a public GitHub repository to start monitoring it.'}
            action={!search && !status && <Link to="/scan" className={buttonClasses('primary')}>Scan a repository</Link>}
          />
        </Card>
      ) : (
        <Card className={cn('overflow-hidden transition-opacity', isFetching && 'opacity-80')}>
          <ul className="divide-y divide-border">
            {data.map((r) => (
              <li key={r.id}>
                <Link to={`/repositories/${r.id}`} className="grid grid-cols-[auto_1fr] items-center gap-4 px-5 py-4 transition-colors hover:bg-surface-2/60 md:grid-cols-[auto_1fr_14rem_11rem]">
                  {r.status === 'Completed' ? (
                    <RiskGradeBadge grade={r.riskGrade} />
                  ) : (
                    <span className="grid size-10 place-items-center rounded-lg border border-border bg-surface-2 text-fg-subtle"><FolderGit2 className="size-5" aria-hidden /></span>
                  )}
                  <div className="min-w-0">
                    <p className="truncate font-medium text-fg">
                      <span className="text-fg-muted">{r.repositoryOwner}/</span>{r.repositoryName}
                    </p>
                    <div className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-fg-subtle">
                      <StatusBadge status={r.status} />
                      {r.language && <span>{r.language}</span>}
                      {r.stars !== null && <span className="inline-flex items-center gap-1"><Star className="size-3" aria-hidden />{formatCompact(r.stars)}</span>}
                      {r.status === 'Completed' && <span>{r.totalDependencies} deps</span>}
                    </div>
                  </div>
                  <div className="col-span-2 md:col-span-1">
                    {r.status === 'Completed' ? (
                      <>
                        <SeverityBar counts={{ Critical: r.criticalVulnerabilities, High: r.highVulnerabilities, Medium: r.mediumVulnerabilities, Low: r.lowVulnerabilities }} showLegend={false} height="sm" />
                        <p className="mt-1.5 text-xs text-fg-muted">
                          {r.totalVulnerabilities === 0 ? 'No known vulnerabilities' : `${r.criticalVulnerabilities} critical · ${r.highVulnerabilities} high · ${r.totalVulnerabilities} total`}
                        </p>
                      </>
                    ) : r.status === 'Failed' ? (
                      <p className="line-clamp-2 text-xs text-danger">{r.errorMessage}</p>
                    ) : null}
                  </div>
                  <p className="col-span-2 text-xs whitespace-nowrap text-fg-subtle md:col-span-1 md:text-right">
                    {r.lastAnalysedAt ? `Scanned ${relativeTime(r.lastAnalysedAt)}` : `Added ${relativeTime(r.createdAt)}`}
                  </p>
                </Link>
              </li>
            ))}
          </ul>
        </Card>
      )}
    </div>
  )
}
