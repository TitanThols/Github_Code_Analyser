import { ArrowRight, Wrench } from 'lucide-react'
import { remediationPlan } from '../../lib/filters'
import { ecosystemLabel } from '../../lib/severity'
import { Card, CardHeader } from '../ui/Card'
import { SeverityBadge } from '../ui/Badges'
import type { Vulnerability } from '../../types'

/** "What should I do first?" — upgrades that clear the most risk, deduplicated per package. */
export function RemediationPlan({ vulnerabilities }: { vulnerabilities: Vulnerability[] }) {
  const plan = remediationPlan(vulnerabilities).slice(0, 6)
  if (plan.length === 0) return null

  return (
    <Card>
      <CardHeader
        title={<span className="inline-flex items-center gap-2"><Wrench className="size-4 text-fg-subtle" aria-hidden />Fix first</span>}
        description="Upgrades that resolve the most severe findings"
      />
      <ul className="mt-3 divide-y divide-border">
        {plan.map((step) => (
          <li key={`${step.ecosystem}:${step.packageName}`} className="flex flex-wrap items-center gap-x-3 gap-y-1 px-5 py-3">
            <SeverityBadge severity={step.worst} />
            <span className="min-w-0 flex-1 truncate font-medium text-fg">
              {step.packageName} <span className="text-xs font-normal text-fg-subtle">{ecosystemLabel[step.ecosystem]}</span>
            </span>
            <span className="inline-flex items-center gap-1.5 font-mono text-xs">
              <span className="text-fg-subtle">{step.from}</span>
              <ArrowRight className="size-3 text-fg-subtle" aria-hidden />
              <span className="text-fg">{step.to}</span>
            </span>
            <span className="w-full text-xs text-fg-subtle sm:w-auto">fixes {step.fixes}</span>
          </li>
        ))}
      </ul>
    </Card>
  )
}
