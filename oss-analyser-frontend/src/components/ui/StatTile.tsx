import type { ReactNode } from 'react'
import { cn } from '../../lib/cn'

interface StatTileProps {
  label: string
  value: ReactNode
  hint?: ReactNode
  icon?: ReactNode
  className?: string
}

/** A headline number. When the job is "one number", a tile beats a chart. */
export function StatTile({ label, value, hint, icon, className }: StatTileProps) {
  return (
    <div className={cn('rounded-2xl border border-border bg-surface p-5 shadow-card', className)}>
      <div className="flex items-center justify-between gap-2">
        <p className="text-sm font-medium text-fg-muted">{label}</p>
        {icon && <span className="text-fg-subtle">{icon}</span>}
      </div>
      <p className="mt-2 text-3xl font-semibold tracking-tight tabular-nums text-fg">{value}</p>
      {hint && <p className="mt-1 text-sm text-fg-subtle">{hint}</p>}
    </div>
  )
}
