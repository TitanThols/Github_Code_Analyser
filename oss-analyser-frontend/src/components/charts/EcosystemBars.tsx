import { ecosystemLabel } from '../../lib/severity'
import { formatNumber } from '../../lib/format'
import type { Dashboard } from '../../types'

/** Magnitude per ecosystem: one hue, sorted, value labels at the bar end. */
export function EcosystemBars({ ecosystems }: { ecosystems: Dashboard['ecosystems'] }) {
  const max = Math.max(1, ...ecosystems.map((e) => e.dependencies))
  return (
    <ul className="space-y-3">
      {ecosystems.map((e) => (
        <li key={e.ecosystem} title={`${ecosystemLabel[e.ecosystem]}: ${e.dependencies} dependencies, ${e.vulnerabilities} vulnerabilities`}>
          <div className="mb-1 flex items-baseline justify-between gap-2 text-sm">
            <span className="font-medium text-fg">{ecosystemLabel[e.ecosystem]}</span>
            <span className="tabular-nums text-fg-muted">
              {formatNumber(e.dependencies)} deps
              {e.vulnerabilities > 0 && <span className="text-fg"> · {formatNumber(e.vulnerabilities)} vulns</span>}
            </span>
          </div>
          <div className="h-2 rounded-full bg-surface-2">
            <div className="h-full rounded-full bg-accent transition-[width] duration-500" style={{ width: `${Math.max(2, (e.dependencies / max) * 100)}%` }} />
          </div>
        </li>
      ))}
    </ul>
  )
}
