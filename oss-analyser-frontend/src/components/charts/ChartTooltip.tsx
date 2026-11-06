import { formatNumber } from '../../lib/format'

export interface TooltipRow {
  label: string
  value: number
  color: string
}

/** Shared tooltip body: neutral surface, values in text ink, a colour key beside each row. */
export function ChartTooltipCard({ title, rows, footer }: { title: string; rows: TooltipRow[]; footer?: string }) {
  return (
    <div className="min-w-40 rounded-lg border border-border bg-surface px-3 py-2 text-sm shadow-pop">
      <p className="mb-1.5 font-medium text-fg">{title}</p>
      <ul className="space-y-1">
        {rows.map((row) => (
          <li key={row.label} className="flex items-center gap-2">
            <span className="size-2.5 shrink-0 rounded-[3px]" style={{ backgroundColor: row.color }} aria-hidden />
            <span className="text-fg-muted">{row.label}</span>
            <span className="ml-auto pl-3 font-medium tabular-nums text-fg">{formatNumber(row.value)}</span>
          </li>
        ))}
      </ul>
      {footer && <p className="mt-1.5 border-t border-border pt-1.5 text-xs text-fg-subtle">{footer}</p>}
    </div>
  )
}
