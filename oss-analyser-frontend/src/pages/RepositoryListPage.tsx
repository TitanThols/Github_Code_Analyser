import { useEffect, useState } from "react";
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
    if (isLoading) return <p>Loading...</p>;
    if (!repos) return <p>Repositories not found</p>;

    return (
        <div className="repo-list-page">
            <h2>Repositories List</h2>
            <div className="repo-list">
                {repos.map((repo) => (
                    <div key={repo.id} className="repo-item">
                        <span>{repo.repositoryUrl}/{repo.repositoryName}</span>
                        <span>{repo.status}</span>
                    </div>
                ))}
            </div>
        </div>
    )
};

export default RepositoryListPage;
