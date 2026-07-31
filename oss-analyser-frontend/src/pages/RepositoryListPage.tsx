import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import type { Repository } from "../types";
import { listRepositories } from "../api/repositories";

const RepositoryListPage = () => {
    const [repos, setRepos] = useState<Repository[]>([]);
    const [isLoading, setIsLoading] = useState(true);

    useEffect(() => {
        const fetchData = async () => {
            try {
                const res = await listRepositories();
                setRepos(res);
            } catch (err) {
                console.error("Failed to fetch", err);
            } finally {
                setIsLoading(false);
            }
        }
        fetchData();
    }, []);

    if (isLoading) return <div className="loading-state">Loading repositories…</div>;

    return (
        <div className="dashboard-page">
            <div className="hero-card">
                <div>
                    <p className="eyebrow">Repository overview</p>
                    <h2>All Analyzed Repositories</h2>
                    <p className="hero-text">{repos.length} repositories tracked</p>
                </div>
            </div>

            <div className="repo-list">
                {repos.length === 0 ? (
                    <div className="card">
                        <p className="muted">No repositories analyzed yet. Go to Analyze to scan one.</p>
                    </div>
                ) : repos.map((repo) => (
                    <Link to={`/repository/${repo.id}`} key={repo.id} style={{ textDecoration: 'none', color: 'inherit' }}>
                        <div className="repo-item">
                            <div>
                                <strong>{repo.repositoryName ?? repo.repositoryOwner ?? 'Repository'}</strong>
                                <p>{repo.repositoryUrl ?? repo.gitHubUrl}</p>
                            </div>
                            <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                                <span className={`repo-status ${repo.status.toLowerCase()}`}>{repo.status}</span>
                            </div>
                        </div>
                    </Link>
                ))}
            </div>
        </div>
    );
};

export default RepositoryListPage;
