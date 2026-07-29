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
            } finally {
                setIsLoading(false);
            }
        };
        fetchData();
    }, [id]);
    if (isLoading) return <p>Loading...</p>;
    if (!repo) return <p>Repository not found</p>;

    return (
        <div className="detail=page">
            <h2>
                {repo.repositoryName}
            </h2>
            <p>Status: {repo.status}</p>
            <h3>Dependecies ({dependencies.length})</h3>
            <table className="data-table">
                <thead>
                    <tr>
                        <th>Package</th>
                        <th>Version</th>
                        <th>Type</th>
                        <th>Vulnerabilities</th>
                    </tr>
                </thead>
                <tbody>
                    {dependencies.map((dep) => (
                        <tr key={dep.id}>
                            <td>{dep.packageName}</td>
                            <td>{dep.type}</td>
                            <td>{dep.vulnerabilityCount}</td>
                        </tr>
                    ))}
                </tbody>
            </table>

            <h3>Vulnerabilities ({vulnerabilities.length})</h3>
            <div className="vuln-list">
                {vulnerabilities.map((vuln) => (
                    <div key={vuln.id} className="vuln-card">
                        <div className="vuln-header">
                            <strong>{vuln.cveId}</strong>
                            <Badge severity={vuln.severity} />
                        </div>
                        <p>{vuln.title}</p>
                        <p>Risk Score: {vuln.riskScore}</p>
                    </div>
                ))}
            </div>
        </div>
    );
};

export default RepositoryDetailPage;