import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { AlertTriangle, ArrowLeft, Download, ExternalLink, FileJson, RefreshCw, Star, Trash2 } from 'lucide-react'
import { useDeleteRepository, useDependencies, useRepository, useRescan, useSnapshots, useVulnerabilities } from '../hooks/queries'
import { useToast } from '../context/ToastContext'
import { ApiError, apiUrl } from '../lib/api'
import { repositoriesApi } from '../api/endpoints'
import { downloadText, vulnerabilitiesToCsv } from '../lib/csv'
import { formatCompact, formatNumber, relativeTime } from '../lib/format'
import { gradeDescription, isInProgress } from '../lib/severity'
import { cn } from '../lib/cn'
import { Card, CardHeader } from '../components/ui/Card'
import { Button } from '../components/ui/Button'
import { buttonClasses } from '../components/ui/buttonStyles'
import { ConfirmDialog } from '../components/ui/ConfirmDialog'
import { EmptyState, ErrorState, Skeleton } from '../components/ui/Feedback'
import { RiskGradeBadge, StatusBadge } from '../components/ui/Badges'
import { StatTile } from '../components/ui/StatTile'
import { SeverityBar } from '../components/charts/SeverityBar'
import { RiskHistoryChart } from '../components/charts/RiskHistoryChart'
import { ScanProgress } from '../components/scan/ScanProgress'
import { VulnerabilityList } from '../components/repository/VulnerabilityList'
import { DependencyTable } from '../components/repository/DependencyTable'
import { RemediationPlan } from '../components/repository/RemediationPlan'
import { NotFoundPage } from './NotFoundPage'

type Tab = 'vulnerabilities' | 'dependencies'

export function RepositoryDetailPage() {
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const toast = useToast()
  const [tab, setTab] = useState<Tab>('vulnerabilities')
  const [confirmDelete, setConfirmDelete] = useState(false)

  const repository = useRepository(id)
  const completed = repository.data?.status === 'Completed'
  const vulnerabilities = useVulnerabilities(id, completed)
  const dependencies = useDependencies(id, completed)
  const snapshots = useSnapshots(id, completed)
  const rescan = useRescan()
  const remove = useDeleteRepository()

  if (repository.isPending) return <DetailSkeleton />
  if (repository.isError) {
    if (repository.error instanceof ApiError && repository.error.status === 404) return <NotFoundPage />
    return <ErrorState message={repository.error.message} onRetry={() => void repository.refetch()} />
  }

  const r = repository.data
  const running = isInProgress(r.status)
  const fullName = `${r.repositoryOwner}/${r.repositoryName}`

  const startRescan = () =>
    rescan.mutate(r.id, {
      onSuccess: () => toast({ tone: 'info', title: 'Re-scan started', description: fullName }),
      onError: (e) => toast({ tone: 'error', title: 'Could not start re-scan', description: e.message }),
    })

  const deleteRepository = () =>
    remove.mutate(r.id, {
      onSuccess: () => {
        toast({ tone: 'success', title: `Removed ${fullName}` })
        navigate('/repositories')
      },
      onError: (e) => {
        setConfirmDelete(false)
        toast({ tone: 'error', title: 'Could not delete repository', description: e.message })
      },
    })

  return (
    <div className="space-y-6">
      <Link to="/repositories" className="inline-flex items-center gap-1.5 text-sm text-fg-muted hover:text-fg">
        <ArrowLeft className="size-4" aria-hidden /> Repositories
      </Link>

      <header className="flex flex-col gap-5 lg:flex-row lg:items-start lg:justify-between">
        <div className="flex min-w-0 items-start gap-4">
          {completed && <RiskGradeBadge grade={r.riskGrade} size="lg" />}
          <div className="min-w-0">
            <h1 className="truncate text-2xl font-semibold tracking-tight text-fg sm:text-[28px]">
              <span className="text-fg-muted">{r.repositoryOwner}/</span>{r.repositoryName}
            </h1>
            {r.description && <p className="mt-1 line-clamp-2 max-w-2xl text-fg-muted">{r.description}</p>}
            <div className="mt-2 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-fg-subtle">
              <StatusBadge status={r.status} />
              {r.language && <span>{r.language}</span>}
              {r.stars !== null && <span className="inline-flex items-center gap-1"><Star className="size-3.5" aria-hidden />{formatCompact(r.stars)}</span>}
              {r.lastAnalysedAt && <span>Scanned {relativeTime(r.lastAnalysedAt)}</span>}
              <a href={r.gitHubUrl} target="_blank" rel="noreferrer" className="inline-flex items-center gap-1 hover:text-fg">
                GitHub <ExternalLink className="size-3.5" aria-hidden />
              </a>
            </div>
          </div>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button variant="secondary" icon={<RefreshCw className={cn('size-4', running && 'animate-spin')} />} onClick={startRescan} disabled={running} loading={rescan.isPending}>
            {running ? 'Scanning…' : 'Re-scan'}
          </Button>
          {completed && (
            <>
              <a href={apiUrl(repositoriesApi.sbomUrl(r.id))} className={buttonClasses('secondary')} download>
                <FileJson className="size-4" aria-hidden /> SBOM
              </a>
              <Button
                variant="secondary"
                icon={<Download className="size-4" />}
                disabled={!vulnerabilities.data?.length}
                onClick={() => vulnerabilities.data && downloadText(`${r.repositoryOwner}-${r.repositoryName}-vulnerabilities.csv`, vulnerabilitiesToCsv(vulnerabilities.data))}
              >
                CSV
              </Button>
            </>
          )}
          <Button variant="danger" icon={<Trash2 className="size-4" />} onClick={() => setConfirmDelete(true)} aria-label="Delete repository" />
        </div>
      </header>

      {running && (
        <Card className="p-5">
          <h2 className="mb-4 text-[15px] font-semibold text-fg">Scan in progress</h2>
          <ScanProgress status={r.status} />
        </Card>
      )}

      {r.status === 'Failed' && (
        <Card className="flex items-start gap-3 border-danger/40 p-5" role="alert">
          <AlertTriangle className="mt-0.5 size-5 shrink-0 text-danger" aria-hidden />
          <div>
            <h2 className="font-semibold text-fg">Scan failed</h2>
            <p className="mt-1 text-sm text-fg-muted">{r.errorMessage ?? 'The analysis stopped unexpectedly.'}</p>
          </div>
        </Card>
      )}

      {completed && (
        <>
          {r.errorMessage && (
            <p className="flex items-start gap-2 rounded-xl border border-border bg-surface px-4 py-3 text-sm text-fg-muted">
              <AlertTriangle className="mt-0.5 size-4 shrink-0 text-sev-medium" aria-hidden />
              {r.errorMessage}
            </p>
          )}

          <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
            <StatTile label="Risk score" value={r.riskScore.toFixed(0)} hint={`Grade ${r.riskGrade} · ${gradeDescription[r.riskGrade]}`} />
            <StatTile label="Vulnerabilities" value={formatNumber(r.totalVulnerabilities)} hint={`${r.criticalVulnerabilities} critical · ${r.highVulnerabilities} high`} />
            <StatTile label="Dependencies" value={formatNumber(r.totalDependencies)} hint={`From ${r.manifestCount} manifest${r.manifestCount === 1 ? '' : 's'}`} />
            <StatTile
              label="Actively exploited"
              value={vulnerabilities.data ? formatNumber(vulnerabilities.data.filter((v) => v.isExploitable).length) : '–'}
              hint="Listed in CISA KEV"
            />
          </div>

          <div className="grid gap-6 lg:grid-cols-3">
            <Card className="lg:col-span-2">
              <CardHeader title="Risk score history" description="One point per scan; re-scanned daily" />
              <div className="px-3 pb-4 pt-4 sm:px-5">
                {snapshots.data && snapshots.data.length > 1 ? (
                  <RiskHistoryChart snapshots={snapshots.data} />
                ) : (
                  <EmptyState title="History builds up over time" description="Tomorrow's automatic re-scan adds the next point." className="py-10" />
                )}
              </div>
            </Card>
            <Card>
              <CardHeader title="Severity mix" />
              <div className="p-5">
                <SeverityBar counts={{ Critical: r.criticalVulnerabilities, High: r.highVulnerabilities, Medium: r.mediumVulnerabilities, Low: r.lowVulnerabilities }} />
              </div>
            </Card>
          </div>

          {vulnerabilities.data && <RemediationPlan vulnerabilities={vulnerabilities.data} />}

          <Card className="overflow-hidden">
            <div className="flex gap-1 border-b border-border px-3 pt-2" role="tablist" aria-label="Repository details">
              {([
                ['vulnerabilities', `Vulnerabilities`, r.totalVulnerabilities],
                ['dependencies', `Dependencies`, r.totalDependencies],
              ] as const).map(([key, label, count]) => (
                <button
                  key={key}
                  type="button"
                  role="tab"
                  id={`tab-${key}`}
                  aria-selected={tab === key}
                  aria-controls={`panel-${key}`}
                  onClick={() => setTab(key)}
                  className={cn(
                    '-mb-px inline-flex items-center gap-2 border-b-2 px-3 py-2.5 text-sm font-medium transition-colors',
                    tab === key ? 'border-accent text-fg' : 'border-transparent text-fg-muted hover:text-fg',
                  )}
                >
                  {label}
                  <span className="rounded-full bg-surface-2 px-2 py-0.5 text-xs tabular-nums text-fg-muted">{formatNumber(count)}</span>
                </button>
              ))}
            </div>
            <div role="tabpanel" id={`panel-${tab}`} aria-labelledby={`tab-${tab}`}>
              {tab === 'vulnerabilities' ? (
                vulnerabilities.isPending ? <div className="p-4"><Skeleton className="h-40" /></div>
                  : vulnerabilities.isError ? <div className="p-4"><ErrorState message={vulnerabilities.error.message} onRetry={() => void vulnerabilities.refetch()} /></div>
                  : <VulnerabilityList vulnerabilities={vulnerabilities.data} />
              ) : dependencies.isPending ? <div className="p-4"><Skeleton className="h-40" /></div>
                : dependencies.isError ? <div className="p-4"><ErrorState message={dependencies.error.message} onRetry={() => void dependencies.refetch()} /></div>
                : <DependencyTable dependencies={dependencies.data} />}
            </div>
          </Card>
        </>
      )}

      <ConfirmDialog
        open={confirmDelete}
        title={`Delete ${fullName}?`}
        description="This removes the repository, its findings and its scan history. You can scan it again later."
        confirmLabel="Delete repository"
        loading={remove.isPending}
        onConfirm={deleteRepository}
        onCancel={() => setConfirmDelete(false)}
      />
    </div>
  )
}

function DetailSkeleton() {
  return (
    <div className="space-y-6" aria-busy="true" aria-label="Loading repository">
      <Skeleton className="h-5 w-28" />
      <div className="flex gap-4"><Skeleton className="size-16 rounded-2xl" /><div className="space-y-2"><Skeleton className="h-8 w-72" /><Skeleton className="h-4 w-96 max-w-full" /></div></div>
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">{Array.from({ length: 4 }, (_, i) => <Skeleton key={i} className="h-[118px] rounded-2xl" />)}</div>
      <Skeleton className="h-96 rounded-2xl" />
    </div>
  )
}
