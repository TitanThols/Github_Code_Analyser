import { useMemo, useState } from 'react'
import { Package, Search } from 'lucide-react'
import { defaultDependencyFilter, filterDependencies, type DependencyFilter } from '../../lib/filters'
import { ecosystemLabel } from '../../lib/severity'
import { EmptyState } from '../ui/Feedback'
import { SeverityBadge } from '../ui/Badges'
import type { Dependency, Ecosystem } from '../../types'

export function DependencyTable({ dependencies }: { dependencies: Dependency[] }) {
  const [filter, setFilter] = useState<DependencyFilter>(defaultDependencyFilter)
  const visible = useMemo(() => filterDependencies(dependencies, filter), [dependencies, filter])
  const ecosystems = useMemo(() => [...new Set(dependencies.map((d) => d.type))].sort(), [dependencies])

  if (dependencies.length === 0) return <EmptyState icon={<Package className="size-6" />} title="No dependencies found" />

  return (
    <div>
      <div className="flex flex-col gap-3 border-b border-border p-4 sm:flex-row sm:items-center">
        <div className="relative flex-1">
          <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-fg-subtle" aria-hidden />
          <input
            type="search"
            value={filter.query}
            onChange={(e) => setFilter((f) => ({ ...f, query: e.target.value }))}
            placeholder="Search packages or manifest paths"
            aria-label="Search dependencies"
            className="h-9 w-full rounded-lg border border-border bg-surface pl-9 pr-3 text-sm text-fg outline-none placeholder:text-fg-subtle focus:border-accent"
          />
        </div>
        <select
          value={filter.ecosystem}
          onChange={(e) => setFilter((f) => ({ ...f, ecosystem: e.target.value as Ecosystem | 'all' }))}
          aria-label="Filter by ecosystem"
          className="h-9 rounded-lg border border-border bg-surface px-3 text-sm text-fg outline-none focus:border-accent"
        >
          <option value="all">All ecosystems</option>
          {ecosystems.map((e) => (
            <option key={e} value={e}>{ecosystemLabel[e]}</option>
          ))}
        </select>
        <label className="inline-flex items-center gap-2 text-sm text-fg-muted">
          <input type="checkbox" checked={filter.vulnerableOnly} onChange={(e) => setFilter((f) => ({ ...f, vulnerableOnly: e.target.checked }))} className="size-4 accent-[var(--accent)]" />
          Vulnerable only
        </label>
      </div>

      <div className="overflow-x-auto">
        <table className="w-full min-w-[640px] text-sm">
          <caption className="sr-only">Dependencies, most severe first</caption>
          <thead>
            <tr className="border-b border-border text-left text-xs font-medium text-fg-subtle">
              <th scope="col" className="px-4 py-2.5 font-medium">Package</th>
              <th scope="col" className="px-4 py-2.5 font-medium">Declared</th>
              <th scope="col" className="px-4 py-2.5 font-medium">Matched</th>
              <th scope="col" className="px-4 py-2.5 font-medium">Manifest</th>
              <th scope="col" className="px-4 py-2.5 text-right font-medium">Findings</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {visible.map((d) => (
              <tr key={d.id} className="hover:bg-surface-2/50">
                <td className="px-4 py-3">
                  <div className="flex items-center gap-2">
                    <span className="font-medium text-fg">{d.packageName}</span>
                    {d.isDevDependency && <span className="rounded border border-border px-1.5 text-[11px] text-fg-subtle">dev</span>}
                  </div>
                  <span className="text-xs text-fg-subtle">{ecosystemLabel[d.type]}</span>
                </td>
                <td className="px-4 py-3 font-mono text-xs text-fg-muted">{d.currentVersion}</td>
                <td className="px-4 py-3 font-mono text-xs">
                  {d.resolvedVersion ? <span className="text-fg">{d.resolvedVersion}</span> : <span className="text-fg-subtle" title="This constraint can't be pinned to a version (e.g. a git URL or '*'), so it isn't matched against advisories.">unpinned</span>}
                </td>
                <td className="max-w-[16rem] truncate px-4 py-3 font-mono text-xs text-fg-subtle" title={d.manifestPath ?? undefined}>{d.manifestPath ?? '—'}</td>
                <td className="px-4 py-3 text-right">
                  {d.highestSeverity ? (
                    <span className="inline-flex items-center gap-2">
                      <span className="tabular-nums text-fg-muted">{d.vulnerabilityCount}</span>
                      <SeverityBadge severity={d.highestSeverity} />
                    </span>
                  ) : (
                    <span className="text-xs text-fg-subtle">None</span>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p className="px-4 py-3 text-xs text-fg-subtle">Showing {visible.length} of {dependencies.length} dependencies</p>
    </div>
  )
}
