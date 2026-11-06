import { CheckCircle2, CircleDashed, Flame, Loader2, XCircle } from 'lucide-react'
import { cn } from '../../lib/cn'
import { gradeDescription, isInProgress, severityColor, statusLabel } from '../../lib/severity'
import type { AnalysisStatus, RiskGrade, Severity } from '../../types'

/** Severity is never colour-alone: a swatch plus the word, in text-coloured ink. */
export function SeverityBadge({ severity, className }: { severity: Severity; className?: string }) {
  return (
    <span
      className={cn('inline-flex items-center gap-1.5 rounded-md px-2 py-0.5 text-xs font-medium text-fg', className)}
      style={{ backgroundColor: `color-mix(in oklab, ${severityColor[severity]} 14%, transparent)` }}
    >
      <span className="size-2 shrink-0 rounded-[3px]" style={{ backgroundColor: severityColor[severity] }} aria-hidden />
      {severity}
    </span>
  )
}

export function StatusBadge({ status }: { status: AnalysisStatus }) {
  const running = isInProgress(status)
  const Icon = status === 'Completed' ? CheckCircle2 : status === 'Failed' ? XCircle : status === 'Pending' ? CircleDashed : Loader2
  return (
    <span className="inline-flex items-center gap-1.5 text-xs font-medium text-fg-muted">
      <Icon
        className={cn('size-3.5', status === 'Completed' && 'text-good', status === 'Failed' && 'text-danger', running && 'text-accent', status !== 'Pending' && running && 'animate-spin')}
        aria-hidden
      />
      {statusLabel[status]}
    </span>
  )
}

const gradeTone: Record<RiskGrade, string> = {
  A: 'var(--good)',
  B: 'var(--sev-low)',
  C: 'var(--sev-medium)',
  D: 'var(--sev-high)',
  F: 'var(--sev-critical)',
}

export function RiskGradeBadge({ grade, size = 'md', className }: { grade: RiskGrade; size?: 'sm' | 'md' | 'lg'; className?: string }) {
  const dimensions = { sm: 'size-7 text-sm rounded-md', md: 'size-10 text-lg rounded-lg', lg: 'size-16 text-3xl rounded-2xl' }[size]
  return (
    <span
      role="img"
      aria-label={`Risk grade ${grade}: ${gradeDescription[grade]}`}
      title={gradeDescription[grade]}
      className={cn('inline-grid shrink-0 place-items-center border-2 font-bold text-fg', dimensions, className)}
      style={{ borderColor: gradeTone[grade], backgroundColor: `color-mix(in oklab, ${gradeTone[grade]} 12%, transparent)` }}
    >
      {grade}
    </span>
  )
}

export function ExploitedBadge() {
  return (
    <span
      className="inline-flex items-center gap-1 rounded-md border border-border px-1.5 py-0.5 text-[11px] font-semibold uppercase tracking-wide text-fg"
      title="Listed in the CISA Known Exploited Vulnerabilities catalog — attackers are using this in the wild"
    >
      <Flame className="size-3 text-sev-critical" aria-hidden />
      Exploited
    </span>
  )
}
