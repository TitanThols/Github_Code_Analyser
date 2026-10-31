import { SEVERITIES, severityColor } from '../../lib/severity'
import { formatNumber } from '../../lib/format'
import type { Severity } from '../../types'

interface SeverityBarProps {
  counts: Record<Severity, number>
  /** Hide the legend when the surrounding UI already labels the counts. */
  showLegend?: boolean
  height?: 'sm' | 'md'
}

/**
 * Part-to-whole of severities as one stacked bar. 2px surface gaps between segments, rounded
 * outer ends, and a legend that doubles as the data table, so nothing relies on colour alone.
 */
export function SeverityBar({ counts, showLegend = true, height = 'md' }: SeverityBarProps) {
  const total = SEVERITIES.reduce((sum, s) => sum + counts[s], 0)
  const summary = total === 0 ? 'No vulnerabilities' : SEVERITIES.map((s) => `${counts[s]} ${s.toLowerCase()}`).join(', ')

  return (
    <div>
      <div role="img" aria-label={summary} className={`flex w-full gap-[2px] overflow-hidden rounded-full bg-surface-2 ${height === 'sm' ? 'h-2' : 'h-3'}`}>
        {total > 0 &&
          SEVERITIES.filter((s) => counts[s] > 0).map((s) => (
            <div
              key={s}
              title={`${s}: ${formatNumber(counts[s])} (${Math.round((counts[s] / total) * 100)}%)`}
              className="h-full transition-[flex-grow] duration-500 first:rounded-l-full last:rounded-r-full"
              style={{ flexGrow: counts[s], flexBasis: 0, minWidth: 4, backgroundColor: severityColor[s] }}
            />
          ))}
      </div>
      {showLegend && (
        <ul className="mt-3 flex flex-wrap gap-x-4 gap-y-1.5 text-sm">
          {SEVERITIES.map((s) => (
            <li key={s} className="flex items-center gap-2">
              <span className="size-2.5 shrink-0 rounded-[3px]" style={{ backgroundColor: severityColor[s] }} aria-hidden />
              <span className="text-fg-muted">{s}</span>
              <span className="font-medium tabular-nums text-fg">{formatNumber(counts[s])}</span>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
