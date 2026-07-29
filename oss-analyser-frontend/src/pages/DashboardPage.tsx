import { useEffect, useState } from 'react';
import { getStats, listRepositories } from '../api/repositories';
import type { DashboardStats, Repository } from '../types';
import Card from '../components/ui/Card';

const DashboardPage = () => {
    const [stats, setStats] = useState<DashboardStats | null>(null);
    const [repos, setRepos] = useState<Repository[]>([]);
    const [isLoading, setIsLoading] = useState(true);

    useEffect(() => {
        const fetchData = async () => {
            try {
                const [statsData, reposData] = await Promise.all([
                    getStats(),
                    listRepositories()
                ]);
                setStats(statsData);
                setRepos(reposData);
            } catch (err) {
                console.error('Failed to fetch dashboard data', err);
            } finally {
                setIsLoading(false);
            }
        };
        fetchData();
    }, []);

    if (isLoading) return <p>Loading...</p>;

    return (
        <div className="dashboard-page">
            <h2>Dashboard</h2>

            <div className="stats-grid">
                <Card title="Total Repositories">
                    <p className="stat-number">{stats?.totalRepositories ?? 0}</p>
                </Card>
                <Card title="Critical">
                    <p className="stat-number">{stats?.totalCritical ?? 0}</p>
                </Card>
                <Card title="High">
                    <p className="stat-number">{stats?.totalHigh ?? 0}</p>
                </Card>
                <Card title="Medium">
                    <p className="stat-number">{stats?.totalMedium ?? 0}</p>
                </Card>
            </div>

            <h3>Recent Repositories</h3>
            <div className="repo-list">
                {repos.map((repo) => (
                    <div key={repo.id} className="repo-item">
                        <span>{repo.repositoryUrl}/{repo.repositoryName}</span>
                        <span>{repo.status}</span>
                    </div>
                ))}
            </div>
        </div>
    );
};

export default DashboardPage;
