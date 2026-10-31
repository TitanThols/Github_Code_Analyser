import { severityRank } from './severity'
import type { Dependency, Ecosystem, Severity, Vulnerability } from '../types'

export interface VulnerabilityFilter {
  query: string
  severities: Severity[]
  fixableOnly: boolean
  exploitedOnly: boolean
  includeDev: boolean
}

export const defaultVulnerabilityFilter: VulnerabilityFilter = {
  query: '',
  severities: [],
  fixableOnly: false,
  exploitedOnly: false,
  includeDev: true,
}

export function filterVulnerabilities(rows: Vulnerability[], filter: VulnerabilityFilter): Vulnerability[] {
  const query = filter.query.trim().toLowerCase()
  return rows
    .filter((v) => filter.severities.length === 0 || filter.severities.includes(v.severity))
    .filter((v) => !filter.fixableOnly || v.remediationVersion !== null)
    .filter((v) => !filter.exploitedOnly || v.isExploitable)
    .filter((v) => filter.includeDev || !v.isDevDependency)
    .filter(
      (v) =>
        !query ||
        v.cveId.toLowerCase().includes(query) ||
        v.packageName.toLowerCase().includes(query) ||
        v.title.toLowerCase().includes(query) ||
        (v.aliases ?? '').toLowerCase().includes(query),
    )
    .sort((a, b) => b.riskScore - a.riskScore || severityRank(b.severity) - severityRank(a.severity))
}

export interface DependencyFilter {
  query: string
  ecosystem: Ecosystem | 'all'
  vulnerableOnly: boolean
}

export const defaultDependencyFilter: DependencyFilter = { query: '', ecosystem: 'all', vulnerableOnly: false }

export function filterDependencies(rows: Dependency[], filter: DependencyFilter): Dependency[] {
  const query = filter.query.trim().toLowerCase()
  return rows.filter(
    (d) =>
      (filter.ecosystem === 'all' || d.type === filter.ecosystem) &&
      (!filter.vulnerableOnly || d.vulnerabilityCount > 0) &&
      (!query || d.packageName.toLowerCase().includes(query) || (d.manifestPath ?? '').toLowerCase().includes(query)),
  )
}

/** Unique upgrade actions: the highest fix version needed per package. */
export function remediationPlan(rows: Vulnerability[]): { packageName: string; ecosystem: Ecosystem; from: string; to: string; fixes: number; worst: Severity }[] {
  const byPackage = new Map<string, { packageName: string; ecosystem: Ecosystem; from: string; to: string; fixes: number; worst: Severity }>()
  for (const v of rows) {
    if (!v.remediationVersion) continue
    const key = `${v.ecosystem}:${v.packageName}`
    const existing = byPackage.get(key)
    if (!existing) {
      byPackage.set(key, { packageName: v.packageName, ecosystem: v.ecosystem, from: v.packageVersion, to: v.remediationVersion, fixes: 1, worst: v.severity })
      continue
    }
    existing.fixes += 1
    if (compareVersions(v.remediationVersion, existing.to) > 0) existing.to = v.remediationVersion
    if (severityRank(v.severity) > severityRank(existing.worst)) existing.worst = v.severity
  }
  return [...byPackage.values()].sort((a, b) => severityRank(b.worst) - severityRank(a.worst) || b.fixes - a.fixes)
}

/** Numeric-aware comparison good enough for ordering fix versions of one package. */
export function compareVersions(a: string, b: string): number {
  return a.localeCompare(b, undefined, { numeric: true, sensitivity: 'base' })
}
