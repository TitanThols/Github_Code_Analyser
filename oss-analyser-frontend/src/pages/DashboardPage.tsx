import { Link } from 'react-router-dom'
import { ArrowRight, Boxes, Flame, FolderGit2, Radar, ShieldAlert, Wrench } from 'lucide-react'
import { useDashboard } from '../hooks/queries'
import { useAuth } from '../context/AuthContext'
import { PageHeader } from '../components/layout/PageHeader'
import { Card, CardHeader } from '../components/ui/Card'
import { StatTile } from '../components/ui/StatTile'
import { EmptyState, ErrorState, Skeleton } from '../components/ui/Feedback'
import { RiskGradeBadge, SeverityBadge } from '../components/ui/Badges'
import { SeverityBar } from '../components/charts/SeverityBar'
import { TrendChart } from '../components/charts/TrendChart'
import { EcosystemBars } from '../components/charts/EcosystemBars'
import { ScanForm } from '../components/scan/ScanForm'
import { formatCompact, formatNumber, pluralize } from '../lib/format'
import { ecosystemLabel } from '../lib/severity'

export function DashboardPage() {
  const { user } = useAuth()
  const { data, isPending, isError, error, refetch } = useDashboard()

  if (isPending) return <DashboardSkeleton />
  if (isError) return <ErrorState message={error.message} onRetry={() => void refetch()} />

  if (data.totalRepositories === 0) {
    return (
      <div className="mx-auto max-w-3xl py-10 text-center sm:py-16">
        <span className="mx-auto grid size-14 place-items-center rounded-2xl bg-accent-soft text-accent">
          <Radar className="size-7" aria-hidden />
        </span>
        <h1 className="mt-6 text-3xl font-semibold tracking-tight text-fg sm:text-4xl">Scan your first repository</h1>
        <p className="mx-auto mt-3 max-w-xl text-fg-muted">
          Paste any public GitHub repository. DepScan finds every manifest, matches each dependency version against OSV.dev, and ranks what to fix first.
        </p>
        <div className="mt-8 text-left">
          <ScanForm autoFocus />
        </div>
      </div>
    )
  }

  const counts = { Critical: data.totalCritical, High: data.totalHigh, Medium: data.totalMedium, Low: data.totalLow }
  const firstName = user?.displayName.split(' ')[0]

  return (
    <div className="space-y-6">
      <PageHeader
        title={firstName ? `Welcome back, ${firstName}` : 'Security overview'}
        description={`${pluralize(data.completedRepositories, 'repository', 'repositories')} monitored${data.inProgressRepositories ? ` · ${data.inProgressRepositories} scanning now` : ''}`}
      />

      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <StatTile label="Open vulnerabilities" value={formatNumber(data.totalVulnerabilities)} hint={`${formatNumber(data.totalCritical + data.totalHigh)} critical or high`} icon={<ShieldAlert className="size-4" />} />
        <StatTile label="Actively exploited" value={formatNumber(data.exploitedVulnerabilities)} hint="In the CISA KEV catalog" icon={<Flame className="size-4" />} />
        <StatTile label="Fix available" value={formatNumber(data.fixableVulnerabilities)} hint={data.totalVulnerabilities ? `${Math.round((data.fixableVulnerabilities / data.totalVulnerabilities) * 100)}% resolvable by upgrading` : 'Nothing to fix'} icon={<Wrench className="size-4" />} />
        <StatTile label="Dependencies tracked" value={formatCompact(data.totalDependencies)} hint={`Across ${pluralize(data.ecosystems.length, 'ecosystem')}`} icon={<Boxes className="size-4" />} />
      </div>

      <div className="grid gap-6 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader title="Open vulnerabilities over time" description="Portfolio total by severity, last 30 days" />
          <div className="px-3 pb-4 pt-4 sm:px-5">
            {data.trend.length > 1 ? (
              <TrendChart data={data.trend} />
            ) : (
              <EmptyState title="Trend appears after the next daily re-scan" description="Each scan records a snapshot; DepScan re-scans every repository daily." className="py-14" />
            )}
          </div>
        </Card>
        <Card>
          <CardHeader title="Severity mix" description={`${formatNumber(data.totalVulnerabilities)} open findings`} />
          <div className="p-5">
            <SeverityBar counts={counts} />
          </div>
          <div className="border-t border-border p-5">
            <h3 className="mb-4 text-sm font-medium text-fg">Ecosystems</h3>
            <EcosystemBars ecosystems={data.ecosystems.slice(0, 6)} />
          </div>
        </Card>
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader
            title="Riskiest repositories"
            action={<Link to="/repositories" className="inline-flex items-center gap-1 text-sm font-medium text-accent hover:underline">View all <ArrowRight className="size-3.5" /></Link>}
          />
          {data.riskiestRepositories.length === 0 ? (
            <EmptyState title="No risky repositories" description="Every completed scan came back clean." icon={<FolderGit2 className="size-6" />} />
          ) : (
            <ul className="mt-3 divide-y divide-border">
              {data.riskiestRepositories.map((r) => (
                <li key={r.id}>
                  <Link to={`/repositories/${r.id}`} className="flex items-center gap-4 px-5 py-3.5 transition-colors hover:bg-surface-2/60">
                    <RiskGradeBadge grade={r.riskGrade} size="sm" />
                    <div className="min-w-0 flex-1">
                      <p className="truncate font-medium text-fg">{r.fullName}</p>
                      <div className="mt-1.5 max-w-xs">
                        <SeverityBar counts={{ Critical: r.critical, High: r.high, Medium: r.medium, Low: r.low }} showLegend={false} height="sm" />
                      </div>
                    </div>
                    <div className="text-right text-xs tabular-nums text-fg-muted">
                      <p className="text-sm font-semibold text-fg">{r.riskScore.toFixed(0)}</p>
                      risk
                    </div>
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </Card>

        <Card>
          <CardHeader title="Most vulnerable packages" description="Across all of your repositories" />
          {data.topVulnerablePackages.length === 0 ? (
            <EmptyState title="No vulnerable packages" icon={<Boxes className="size-6" />} />
          ) : (
            <div className="mt-3 overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-y border-border text-left text-xs text-fg-subtle">
                    <th scope="col" className="px-5 py-2 font-medium">Package</th>
                    <th scope="col" className="px-3 py-2 font-medium">Worst</th>
                    <th scope="col" className="px-3 py-2 text-right font-medium">Advisories</th>
                    <th scope="col" className="px-5 py-2 text-right font-medium">Repos</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {data.topVulnerablePackages.map((p) => (
                    <tr key={`${p.ecosystem}:${p.packageName}`}>
                      <td className="px-5 py-3">
                        <span className="font-medium text-fg">{p.packageName}</span>
                        <span className="ml-2 text-xs text-fg-subtle">{ecosystemLabel[p.ecosystem]}</span>
                      </td>
                      <td className="px-3 py-3"><SeverityBadge severity={p.highestSeverity} /></td>
                      <td className="px-3 py-3 text-right tabular-nums text-fg">{p.vulnerabilities}</td>
                      <td className="px-5 py-3 text-right tabular-nums text-fg-muted">{p.repositories}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Card>
      </div>
    </div>
  )
}

function DashboardSkeleton() {
  return (
    <div className="space-y-6" aria-busy="true" aria-label="Loading dashboard">
      <Skeleton className="h-9 w-72" />
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        {Array.from({ length: 4 }, (_, i) => <Skeleton key={i} className="h-[118px] rounded-2xl" />)}
      </div>
      <div className="grid gap-6 lg:grid-cols-3">
        <Skeleton className="h-80 rounded-2xl lg:col-span-2" />
        <Skeleton className="h-80 rounded-2xl" />
      </div>
    </div>
  )
}
