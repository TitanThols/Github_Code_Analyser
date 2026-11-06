import type { AnalysisStatus, Ecosystem, RiskGrade, Severity } from '../types'

export const SEVERITIES: Severity[] = ['Critical', 'High', 'Medium', 'Low']

export const severityRank = (severity: Severity | null | undefined): number =>
  severity ? 4 - SEVERITIES.indexOf(severity) : 0

/** CSS variable for each severity; values differ per theme (see index.css). */
export const severityColor: Record<Severity, string> = {
  Critical: 'var(--sev-critical)',
  High: 'var(--sev-high)',
  Medium: 'var(--sev-medium)',
  Low: 'var(--sev-low)',
}

export const gradeDescription: Record<RiskGrade, string> = {
  A: 'No known vulnerabilities',
  B: 'Low risk',
  C: 'Moderate risk',
  D: 'High risk',
  F: 'Critical risk',
}

export const isInProgress = (status: AnalysisStatus) =>
  status === 'Pending' || status === 'Parsing' || status === 'FetchingVulnerabilities'

export const statusLabel: Record<AnalysisStatus, string> = {
  Pending: 'Queued',
  Parsing: 'Reading manifests',
  FetchingVulnerabilities: 'Checking advisories',
  Completed: 'Completed',
  Failed: 'Failed',
}

export const ecosystemLabel: Record<Ecosystem, string> = {
  NPM: 'npm',
  PyPI: 'PyPI',
  NuGet: 'NuGet',
  Maven: 'Maven',
  Gradle: 'Gradle',
  Gem: 'RubyGems',
  Go: 'Go',
  Composer: 'Packagist',
  Cargo: 'crates.io',
  Pub: 'Pub',
}
