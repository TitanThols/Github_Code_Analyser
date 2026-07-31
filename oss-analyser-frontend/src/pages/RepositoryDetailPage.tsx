import { useEffect, useState } from "react";
import { useParams } from 'react-router-dom';
import { getRepository, getDependencies, getVulnerabilities } from '../api/repositories';
import type { Repository, Dependency, Vulnerability } from '../types';
import Badge from '../components/ui/Badge';


const RepositoryDetailPage = () => {
    const [repo, setRepo] = useState<Repository | null>(null);
    const [dependencies, setDependencies] = useState<Dependency[]>([]);
    const [vulnerabilities, setVulnerabilities] = useState<Vulnerability[]>([]);
    const [isLoading, setIsLoading] = useState(true);

    const { id } = useParams<{ id: string }>();

    useEffect(() => {
        const fetchData = async () => {
            if (!id) return;
            try {
                const [repoData, depsData, vulnsData] = await Promise.all([
                    getRepository(id),
                    getDependencies(id),
                    getVulnerabilities(id),
                ]);
                setRepo(repoData);
                setDependencies(depsData);
                setVulnerabilities(vulnsData);
            } catch (err) {
                console.error('Failed to fetch repository details', err);
                setRepo(null);
            } finally {
                setIsLoading(false);
            }
        };
        fetchData();
    }, [id]);

    if (isLoading) return <div className="loading-state">Loading repository details…</div>;
    if (!repo) return <div className="card"><h2>Repository not found</h2><p>The repository could not be loaded yet. It may still be processing or the ID may be invalid.</p></div>;

    const displayName = repo.repositoryName ?? repo.repositoryOwner ?? repo.gitHubUrl ?? 'Repository';
    const displayUrl = repo.repositoryUrl ?? repo.gitHubUrl ?? 'Unknown URL';

    return (
        <div className="dashboard-page">
            <div className="hero-card">
                <div>
                    <p className="eyebrow">Repository analysis</p>
                    <h2>{displayName}</h2>
                    <p className="hero-text">{displayUrl}</p>
                </div>
                <div className="connection-pill">
                    <span className="status-dot" />
                    {repo.status}
                </div>
            </div>

            <div className="stats-grid">
                <div className="card">
                    <h3 className="card-title">Dependencies</h3>
                    <p className="stat-number">{repo.totalDependencies ?? dependencies.length}</p>
                </div>
                <div className="card">
                    <h3 className="card-title">Critical</h3>
                    <p className="stat-number critical">{repo.criticalVulnerabilities ?? 0}</p>
                </div>
                <div className="card">
                    <h3 className="card-title">High</h3>
                    <p className="stat-number high">{repo.highVulnerabilities ?? 0}</p>
                </div>
                <div className="card">
                    <h3 className="card-title">Medium</h3>
                    <p className="stat-number medium">{repo.mediumVulnerabilities ?? 0}</p>
                </div>
            </div>

            <div className="dashboard-bottom">
                <div className="card">
                    <h3 className="card-title">Dependencies ({dependencies.length})</h3>
                    <div className="repo-list">
                        {dependencies.map((dep) => (
                            <div key={dep.id} className="repo-item">
                                <div>
                                    <strong>{dep.packageName}</strong>
                                    <p>{dep.currentVersion}</p>
                                </div>
                                <span className="repo-status">{dep.type}</span>
                            </div>
                        ))}
                    </div>
                </div>

                <div className="card">
                    <h3 className="card-title">Vulnerabilities ({vulnerabilities.length})</h3>
                    <div className="alerts-list">
                        {vulnerabilities.map((vuln) => (
                            <div key={vuln.id} className="alert-item">
                                <div>
                                    <strong>{vuln.cveId}</strong>
                                    <p>{vuln.title}</p>
                                </div>
                                <Badge severity={vuln.severity} />
                            </div>
                        ))}
                    </div>
                </div>
            </div>
        </div>
    );
};

export default RepositoryDetailPage;