export type AnalysisStatus = 'Pending' | 'Parsing' | 'FetchingVulnerabilities' | 'Completed' | 'Failed'
export type Severity = 'Critical' | 'High' | 'Medium' | 'Low'
export type Ecosystem = 'NPM' | 'PyPI' | 'NuGet' | 'Maven' | 'Gem' | 'Gradle' | 'Pub' | 'Composer' | 'Go' | 'Cargo'
export type RiskGrade = 'A' | 'B' | 'C' | 'D' | 'F'

export interface User {
  id: string
  displayName: string
  email: string
  createdAt: string
}

export interface Repository {
  id: string
  gitHubUrl: string
  repositoryOwner: string
  repositoryName: string
  description: string | null
  language: string | null
  stars: number | null
  status: AnalysisStatus
  createdAt: string
  completedAt: string | null
  lastAnalysedAt: string | null
  totalDependencies: number
  manifestCount: number
  criticalVulnerabilities: number
  highVulnerabilities: number
  mediumVulnerabilities: number
  lowVulnerabilities: number
  totalVulnerabilities: number
  riskScore: number
  riskGrade: RiskGrade
  errorMessage: string | null
}

export interface Dependency {
  id: string
  packageName: string
  currentVersion: string
  resolvedVersion: string | null
  type: Ecosystem
  manifestPath: string | null
  isDevDependency: boolean
  vulnerabilityCount: number
  highestSeverity: Severity | null
}

export interface Vulnerability {
  id: string
  dependencyId: string
  cveId: string
  advisoryId: string | null
  aliases: string | null
  title: string
  description: string
  severity: Severity
  cvssScore: number | null
  cvssVector: string | null
  riskScore: number
  isExploitable: boolean
  exploitUrl: string | null
  affectedVersionRange: string | null
  remediationVersion: string | null
  referenceUrl: string | null
  publishedAt: string
  packageName: string
  packageVersion: string
  ecosystem: Ecosystem
  isDevDependency: boolean
}

export interface Snapshot {
  snapshotDate: string
  criticalCount: number
  highCount: number
  mediumCount: number
  lowCount: number
  totalVulnerabilities: number
  averageRiskScore: number
  riskScore: number
}

export interface Dashboard {
  totalRepositories: number
  completedRepositories: number
  inProgressRepositories: number
  failedRepositories: number
  totalDependencies: number
  totalVulnerabilities: number
  totalCritical: number
  totalHigh: number
  totalMedium: number
  totalLow: number
  exploitedVulnerabilities: number
  fixableVulnerabilities: number
  averageRiskScore: number
  ecosystems: { ecosystem: Ecosystem; dependencies: number; vulnerabilities: number }[]
  riskiestRepositories: { id: string; fullName: string; riskScore: number; riskGrade: RiskGrade; critical: number; high: number; medium: number; low: number }[]
  topVulnerablePackages: { packageName: string; ecosystem: Ecosystem; vulnerabilities: number; repositories: number; highestSeverity: Severity }[]
  trend: { date: string; critical: number; high: number; medium: number; low: number; total: number }[]
}

export interface AlertMessage {
  type: 'ANALYSIS_PROGRESS' | 'ANALYSIS_COMPLETED' | 'ANALYSIS_FAILED' | 'NEW_VULNERABILITY'
  repositoryId: string
  repositoryName: string
  status?: AnalysisStatus
  riskScore?: number
  critical?: number
  high?: number
  message?: string | null
  cveId?: string
  title?: string
  packageName?: string
  severity?: Severity
  timestamp: string
}
