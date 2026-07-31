export type AnalysisStatus = 'Pending' | 'Parsing' | 'FetchingVulnerabilities' | 'Completed' | 'Failed';

export type Severity = 'Low' | 'Medium' | 'High' | 'Critical';

export type DependencyType = 'NPM' | 'PyPI' | 'NuGet' | 'Maven' | 'Gem' | 'Gradle' | 'Pub' | 'Composer';

export interface Repository {
    id: string;
    repositoryName?: string;
    repositoryUrl?: string;
    gitHubUrl?: string;
    repositoryOwner?: string;
    status: AnalysisStatus | string;
    createdAt: string;
    completedAt?: string | null;
    totalDependencies?: number;
    dependenciesCount?: number;
    criticalVulnerabilities?: number;
    highVulnerabilities?: number;
    mediumVulnerabilities?: number;
    errorMessage?: string | null;
}

export interface Dependency {
    id: string,
    packageName: string,
    currentVersion: string,
    latestVersion: string | null,
    type: DependencyType,
    vulnerabilityCount: number,
    vulnerabilities: Vulnerability[]
}

export interface Vulnerability {
    id: string,
    cveId: string,
    title: string,
    description: string,
    severity: Severity,
    cvssScore: number | null,
    riskScore: number,
    isExploitable: boolean,
    affectedVersionRange: string | null,
    remediationVersion: string | null,
    publishedAt: string,
    packageName: string | null
}

export interface VulnerabilitySnapshot {
    snapshotDate: string,
    criticalCount: number,
    highCount: number,
    mediumCount: number,
    lowCount: number,
    totalVulnerabilities: number,
    averageRiskScore: number
}

export interface DashboardStats {
    totalRepositories: number;
    totalDependencies: number;
    totalCritical: number;
    totalHigh: number;
    totalMedium: number;
    totalLow: number;
    averageRiskScore: number;
}
