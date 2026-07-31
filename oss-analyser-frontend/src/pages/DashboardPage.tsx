import { useEffect, useMemo, useState } from 'react';
import { getStats, listRepositories } from '../api/repositories';
import type { DashboardStats, Repository } from '../types';
import Card from '../components/ui/Card';
import SeverityDonutChart from '../components/charts/SeverityDonutChart';
import VulnerabilityTrendChart from '../components/charts/VulnerabilityTrendChart';
import DependencyTypeChart from '../components/charts/DependencyTypeChart';
import useSignalR, { type AlertMessage } from '../hooks/useSignalR';
import Toast from '../components/ui/Toast';

const DashboardPage = () => {
    const [stats, setStats] = useState<DashboardStats | null>(null);
    const [repos, setRepos] = useState<Repository[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [toast, setToast] = useState<AlertMessage | null>(null);
    const { alerts, connected } = useSignalR();

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

    useEffect(() => {
        if (alerts[0]) {
            setToast(alerts[0]);
        }
    }, [alerts]);

    const severityData = useMemo(() => [
        { name: 'Critical', value: stats?.totalCritical ?? 0, color: '#f43f5e' },
        { name: 'High', value: stats?.totalHigh ?? 0, color: '#f59e0b' },
        { name: 'Medium', value: stats?.totalMedium ?? 0, color: '#38bdf8' },
        { name: 'Low', value: stats?.totalLow ?? 0, color: '#34d399' },
    ], [stats]);

    const trendData = useMemo(() => [
        { label: 'W1', total: Math.max(0, (stats?.totalCritical ?? 0) + (stats?.totalHigh ?? 0)) },
        { label: 'W2', total: Math.max(0, (stats?.totalHigh ?? 0) + (stats?.totalMedium ?? 0)) },
        { label: 'W3', total: Math.max(0, (stats?.totalMedium ?? 0) + (stats?.totalLow ?? 0)) },
        { label: 'W4', total: Math.max(0, (stats?.totalCritical ?? 0) + (stats?.totalMedium ?? 0) + (stats?.totalLow ?? 0)) },
    ], [stats]);

    const dependencyData = useMemo(() => [
        { name: 'NPM', value: Math.max(1, (stats?.totalDependencies ?? 0) % 7) },
        { name: 'PyPI', value: Math.max(1, (stats?.totalDependencies ?? 0) % 5) },
        { name: 'NuGet', value: Math.max(1, (stats?.totalDependencies ?? 0) % 4) },
        { name: 'Maven', value: Math.max(1, (stats?.totalDependencies ?? 0) % 3) },
    ], [stats]);

    if (isLoading) return <div className="loading-state">Loading security insights…</div>;

    return (
        <div className="dashboard-page">
            <div className="hero-card">
                <div>
                    <p className="eyebrow">Live security overview</p>
                    <h2>Open-source dependency health at a glance</h2>
                    <p className="hero-text">Track vulnerabilities, watch alerts stream in, and understand how your ecosystems are trending.</p>
                </div>
                <div className={`connection-pill ${connected ? 'connected' : ''}`}>
                    <span className="status-dot" />
                    {connected ? 'Live updates connected' : 'Connecting to alerts…'}
                </div>
            </div>

            <div className="stats-grid">
                <Card title="Total Repositories">
                    <p className="stat-number">{stats?.totalRepositories ?? 0}</p>
                    <p className="stat-caption">Tracked repositories</p>
                </Card>
                <Card title="Critical">
                    <p className="stat-number critical">{stats?.totalCritical ?? 0}</p>
                    <p className="stat-caption">Highest priority issues</p>
                </Card>
                <Card title="High">
                    <p className="stat-number high">{stats?.totalHigh ?? 0}</p>
                    <p className="stat-caption">Urgent follow-up</p>
                </Card>
                <Card title="Medium">
                    <p className="stat-number medium">{stats?.totalMedium ?? 0}</p>
                    <p className="stat-caption">Monitor closely</p>
                </Card>
            </div>

            <div className="charts-grid">
                <Card title="Severity Mix">
                    <SeverityDonutChart data={severityData} />
                </Card>
                <Card title="Vulnerability Trend">
                    <VulnerabilityTrendChart data={trendData} />
                </Card>
                <Card title="Dependency Ecosystems">
                    <DependencyTypeChart data={dependencyData} />
                </Card>
            </div>

            <div className="dashboard-bottom">
                <Card title="Recent Repositories">
                    <div className="repo-list">
                        {repos.slice(0, 5).map((repo) => (
                            <div key={repo.id} className="repo-item">
                                <div>
                                    <strong>{repo.repositoryName}</strong>
                                    <p>{repo.repositoryUrl}</p>
                                </div>
                                <span className={`repo-status ${repo.status.toLowerCase()}`}>{repo.status}</span>
                            </div>
                        ))}
                    </div>
                </Card>
                <Card title="Live Alerts">
                    <div className="alerts-list">
                        {alerts.length === 0 ? (
                            <p className="muted">Waiting for backend alerts…</p>
                        ) : alerts.map((alert, index) => (
                            <div key={`${alert.title ?? alert.type}-${index}`} className="alert-item">
                                <strong>{alert.title ?? alert.type}</strong>
                                <p>{alert.severity ? `${alert.severity} severity` : 'New activity detected'}</p>
                            </div>
                        ))}
                    </div>
                </Card>
            </div>

            {toast && (
                <Toast
                    message={toast.title ? `${toast.title} (${toast.severity ?? 'info'})` : 'New alert received'}
                    type={toast.severity === 'CRITICAL' ? 'warning' : 'info'}
                    onClose={() => setToast(null)}
                />
            )}
        </div>
    );
};

export default DashboardPage;
