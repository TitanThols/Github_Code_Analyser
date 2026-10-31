import { Check, Loader2 } from 'lucide-react'
import { cn } from '../../lib/cn'
import type { AnalysisStatus } from '../../types'

const steps: { status: AnalysisStatus; label: string; detail: string }[] = [
  { status: 'Pending', label: 'Queued', detail: 'Waiting for a worker' },
  { status: 'Parsing', label: 'Reading manifests', detail: 'Discovering and parsing dependency files' },
  { status: 'FetchingVulnerabilities', label: 'Checking advisories', detail: 'Matching versions against OSV.dev and CISA KEV' },
  { status: 'Completed', label: 'Scoring', detail: 'Prioritising findings by real-world risk' },
]

export function ScanProgress({ status }: { status: AnalysisStatus }) {
  const current = steps.findIndex((s) => s.status === status)

  return (
    <ol className="grid gap-3 sm:grid-cols-4" aria-label="Scan progress">
      {steps.map((step, index) => {
        const done = index < current
        const active = index === current
        return (
          <li
            key={step.status}
            aria-current={active ? 'step' : undefined}
            className={cn('rounded-xl border p-4 transition-colors', active ? 'border-accent bg-accent-soft' : 'border-border bg-surface')}
          >
            <div className="flex items-center gap-2">
              <span
                className={cn(
                  'grid size-6 place-items-center rounded-full text-xs font-semibold',
                  done ? 'bg-good text-white' : active ? 'bg-accent text-accent-fg' : 'bg-surface-2 text-fg-subtle',
                )}
              >
                {done ? <Check className="size-3.5" aria-hidden /> : active ? <Loader2 className="size-3.5 animate-spin" aria-hidden /> : index + 1}
              </span>
              <span className={cn('text-sm font-medium', active || done ? 'text-fg' : 'text-fg-subtle')}>{step.label}</span>
            </div>
            <p className="mt-2 text-xs text-fg-muted">{step.detail}</p>
          </li>
        )
      })}
    </ol>
  )
}
