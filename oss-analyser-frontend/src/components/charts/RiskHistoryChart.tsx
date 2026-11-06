import { CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis, type TooltipContentProps } from 'recharts'
import type { NameType, ValueType } from 'recharts/types/component/DefaultTooltipContent'
import { formatShortDate } from '../../lib/format'
import type { Snapshot } from '../../types'

export function RiskTooltip({ active, payload }: TooltipContentProps<ValueType, NameType>) {
  if (!active || !payload?.length) return null
  const s = payload[0].payload as Snapshot
  return (
    <div className="rounded-lg border border-border bg-surface px-3 py-2 text-sm shadow-pop">
      <p className="font-medium text-fg">{formatShortDate(s.snapshotDate)}</p>
      <p className="text-fg-muted">
        Risk score <span className="font-medium tabular-nums text-fg">{s.riskScore.toFixed(1)}</span>
      </p>
      <p className="text-fg-muted">
        Open findings <span className="font-medium tabular-nums text-fg">{s.totalVulnerabilities}</span>
      </p>
    </div>
  )
}

/** One series, so no legend: the card title names it. */
export function RiskHistoryChart({ snapshots, height = 200 }: { snapshots: Snapshot[]; height?: number }) {
  return (
    <div style={{ height }} role="img" aria-label={`Risk score across ${snapshots.length} scans`}>
      <ResponsiveContainer width="100%" height="100%">
        <LineChart data={snapshots} margin={{ top: 8, right: 12, bottom: 0, left: -16 }}>
          <CartesianGrid stroke="var(--chart-grid)" vertical={false} />
          <XAxis
            dataKey="snapshotDate"
            tickFormatter={(v: string) => formatShortDate(v)}
            tick={{ fill: 'var(--fg-subtle)', fontSize: 12 }}
            tickLine={false}
            axisLine={{ stroke: 'var(--border)' }}
            minTickGap={32}
          />
          <YAxis domain={[0, 100]} ticks={[0, 25, 50, 75, 100]} tick={{ fill: 'var(--fg-subtle)', fontSize: 12 }} tickLine={false} axisLine={false} width={48} />
          <Tooltip content={RiskTooltip} cursor={{ stroke: 'var(--border-strong)', strokeWidth: 1 }} />
          <Line
            type="monotone"
            dataKey="riskScore"
            stroke="var(--accent)"
            strokeWidth={2}
            dot={{ r: 4, fill: 'var(--accent)', stroke: 'var(--surface)', strokeWidth: 2 }}
            activeDot={{ r: 5, stroke: 'var(--surface)', strokeWidth: 2 }}
            isAnimationActive={false}
          />
        </LineChart>
      </ResponsiveContainer>
    </div>
  )
}
