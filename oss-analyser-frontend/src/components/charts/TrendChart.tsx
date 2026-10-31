import { Area, AreaChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis, type TooltipContentProps } from 'recharts'
import { SEVERITIES, severityColor } from '../../lib/severity'
import type { NameType, ValueType } from 'recharts/types/component/DefaultTooltipContent'
import { formatShortDate } from '../../lib/format'
import { ChartTooltipCard } from './ChartTooltip'
import type { Severity } from '../../types'

export interface TrendPoint {
  date: string
  critical: number
  high: number
  medium: number
  low: number
}

const keyOf = (s: Severity) => s.toLowerCase() as Lowercase<Severity>

export function TrendTooltip({ active, payload, label }: TooltipContentProps<ValueType, NameType>) {
  if (!active || !payload?.length) return null
  const point = payload[0].payload as TrendPoint
  const total = SEVERITIES.reduce((sum, s) => sum + point[keyOf(s)], 0)
  return (
    <ChartTooltipCard
      title={formatShortDate(String(label))}
      rows={SEVERITIES.map((s) => ({ label: s, value: point[keyOf(s)], color: severityColor[s] }))}
      footer={`${total} open vulnerabilities`}
    />
  )
}

/** Open vulnerabilities over time, stacked by severity (critical on top, nearest the eye). */
export function TrendChart({ data, height = 240 }: { data: TrendPoint[]; height?: number }) {
  const stackOrder = [...SEVERITIES].reverse()
  return (
    <div>
      <div style={{ height }} role="img" aria-label={`Vulnerability trend over ${data.length} days`}>
        <ResponsiveContainer width="100%" height="100%">
          <AreaChart data={data} margin={{ top: 8, right: 8, bottom: 0, left: -16 }}>
            <CartesianGrid stroke="var(--chart-grid)" vertical={false} />
            <XAxis
              dataKey="date"
              tickFormatter={(v: string) => formatShortDate(v)}
              tick={{ fill: 'var(--fg-subtle)', fontSize: 12 }}
              tickLine={false}
              axisLine={{ stroke: 'var(--border)' }}
              minTickGap={32}
            />
            <YAxis allowDecimals={false} tick={{ fill: 'var(--fg-subtle)', fontSize: 12 }} tickLine={false} axisLine={false} width={48} />
            <Tooltip content={TrendTooltip} cursor={{ stroke: 'var(--border-strong)', strokeWidth: 1 }} />
            {stackOrder.map((s) => (
              <Area
                key={s}
                type="monotone"
                dataKey={keyOf(s)}
                name={s}
                stackId="severity"
                stroke={severityColor[s]}
                strokeWidth={2}
                fill={severityColor[s]}
                fillOpacity={0.22}
                activeDot={{ r: 4, strokeWidth: 2, stroke: 'var(--surface)' }}
                isAnimationActive={false}
              />
            ))}
          </AreaChart>
        </ResponsiveContainer>
      </div>
      <ul className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-fg-muted" aria-label="Legend">
        {SEVERITIES.map((s) => (
          <li key={s} className="flex items-center gap-1.5">
            <span className="size-2.5 shrink-0 rounded-[3px]" style={{ backgroundColor: severityColor[s] }} aria-hidden />
            {s}
          </li>
        ))}
      </ul>
    </div>
  )
}
