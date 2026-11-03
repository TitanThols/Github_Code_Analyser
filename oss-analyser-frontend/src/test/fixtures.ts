import type { Dashboard, Dependency, Repository, Snapshot, User, Vulnerability } from '../types'

export const user: User = { id: 'u1', displayName: 'Ada Lovelace', email: 'ada@example.com', createdAt: '2026-01-01T00:00:00Z' }

export const repository = (overrides: Partial<Repository> = {}): Repository => ({
  id: 'r1',
  gitHubUrl: 'https://github.com/acme/shop',
  repositoryOwner: 'acme',
  repositoryName: 'shop',
  description: 'A storefront',
  language: 'TypeScript',
  stars: 1234,
  status: 'Completed',
  createdAt: '2026-01-01T00:00:00Z',
  completedAt: '2026-01-01T00:01:00Z',
  lastAnalysedAt: new Date(Date.now() - 3_600_000).toISOString(),
  totalDependencies: 3,
  manifestCount: 2,
  criticalVulnerabilities: 1,
  highVulnerabilities: 1,
  mediumVulnerabilities: 0,
  lowVulnerabilities: 1,
  totalVulnerabilities: 3,
  riskScore: 96.4,
  riskGrade: 'F',
  errorMessage: null,
  ...overrides,
})

export const vulnerability = (overrides: Partial<Vulnerability> = {}): Vulnerability => ({
  id: 'v1',
  dependencyId: 'd1',
  cveId: 'CVE-2019-10744',
  advisoryId: 'GHSA-jf85-cpcp-j695',
  aliases: 'CVE-2019-10744, SNYK-JS-LODASH-450202',
  title: 'Prototype Pollution in lodash',
  description: 'Versions of lodash before 4.17.12 are vulnerable.',
  severity: 'Critical',
  cvssScore: 9.1,
  cvssVector: 'CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:N/I:H/A:H',
  riskScore: 95.5,
  isExploitable: true,
  exploitUrl: 'https://www.cisa.gov/kev?search=CVE-2019-10744',
  affectedVersionRange: '>=0, <4.17.12',
  remediationVersion: '4.17.12',
  referenceUrl: 'https://osv.dev/vulnerability/GHSA-jf85-cpcp-j695',
  publishedAt: '2019-07-10T00:00:00Z',
  packageName: 'lodash',
  packageVersion: '4.17.4',
  ecosystem: 'NPM',
  isDevDependency: false,
  ...overrides,
})

export const vulnerabilities: Vulnerability[] = [
  vulnerability(),
  vulnerability({ id: 'v2', cveId: 'CVE-2020-8203', title: 'Prototype pollution in zipObjectDeep', severity: 'High', riskScore: 62, isExploitable: false, exploitUrl: null, remediationVersion: '4.17.19', cvssScore: 7.4 }),
  vulnerability({ id: 'v3', cveId: 'GHSA-xxxx-yyyy-zzzz', title: 'ReDoS in test helper', severity: 'Low', riskScore: 17.5, isExploitable: false, exploitUrl: null, remediationVersion: null, cvssScore: null, packageName: 'jest-helper', packageVersion: '1.0.0', isDevDependency: true, aliases: null }),
]

export const dependencies: Dependency[] = [
  { id: 'd1', packageName: 'lodash', currentVersion: '^4.17.4', resolvedVersion: '4.17.4', type: 'NPM', manifestPath: 'package.json', isDevDependency: false, vulnerabilityCount: 2, highestSeverity: 'Critical' },
  { id: 'd2', packageName: 'flask', currentVersion: '==2.0.0', resolvedVersion: '2.0.0', type: 'PyPI', manifestPath: 'api/requirements.txt', isDevDependency: false, vulnerabilityCount: 0, highestSeverity: null },
  { id: 'd3', packageName: 'local-lib', currentVersion: 'file:../lib', resolvedVersion: null, type: 'NPM', manifestPath: 'package.json', isDevDependency: true, vulnerabilityCount: 0, highestSeverity: null },
]

export const snapshots: Snapshot[] = [
  { snapshotDate: '2026-01-01T00:00:00Z', criticalCount: 1, highCount: 0, mediumCount: 0, lowCount: 0, totalVulnerabilities: 1, averageRiskScore: 89, riskScore: 89 },
  { snapshotDate: '2026-01-02T00:00:00Z', criticalCount: 1, highCount: 1, mediumCount: 0, lowCount: 1, totalVulnerabilities: 3, averageRiskScore: 58, riskScore: 96.4 },
]

export const dashboard = (overrides: Partial<Dashboard> = {}): Dashboard => ({
  totalRepositories: 2,
  completedRepositories: 2,
  inProgressRepositories: 0,
  failedRepositories: 0,
  totalDependencies: 1500,
  totalVulnerabilities: 12,
  totalCritical: 2,
  totalHigh: 3,
  totalMedium: 4,
  totalLow: 3,
  exploitedVulnerabilities: 1,
  fixableVulnerabilities: 9,
  averageRiskScore: 70,
  ecosystems: [{ ecosystem: 'NPM', dependencies: 1200, vulnerabilities: 8 }, { ecosystem: 'PyPI', dependencies: 300, vulnerabilities: 4 }],
  riskiestRepositories: [{ id: 'r1', fullName: 'acme/shop', riskScore: 96.4, riskGrade: 'F', critical: 2, high: 1, medium: 0, low: 0 }],
  topVulnerablePackages: [{ packageName: 'lodash', ecosystem: 'NPM', vulnerabilities: 3, repositories: 2, highestSeverity: 'Critical' }],
  trend: [
    { date: '2026-01-01', critical: 1, high: 1, medium: 1, low: 0, total: 3 },
    { date: '2026-01-02', critical: 2, high: 3, medium: 4, low: 3, total: 12 },
  ],
  ...overrides,
})
