import { createContext, useContext, useState, type ReactNode } from "react";

import type { Repository, DashboardStats } from '../types';
import { listRepositories, getStats } from "../api/repositories";

interface AppContextType {
    repositories: Repository[];
    stats: DashboardStats | null;
    isLoading: boolean;
    error: string | null;
    fetchRepositories: () => Promise<void>;
    fetchStats: () => Promise<void>;
}

const AppContext = createContext<AppContextType | undefined>(undefined);

export const AppProvider = ({ children }: { children: ReactNode }) => {
    const [repositories, setRepositories] = useState<Repository[]>([]);
    const [stats, setStats] = useState<DashboardStats | null>(null);
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const fetchRepositories = async () => {
        try {
            setIsLoading(true);
            setError(null);
            const data = await listRepositories();
            setRepositories(data);
        } catch (error) {
            setError("Failed to fetch repositories");
        } finally {
            setIsLoading(false);
        }
    }
    const fetchStats = async () => {
        try {
            const data = await getStats();
            setStats(data);
        } catch (err) {
            setError('Failed to fetch stats');
        }
    };

    return (
        <AppContext.Provider value={{
            repositories,
            stats,
            isLoading,
            error,
            fetchRepositories,
            fetchStats,
        }}>
            {children}
        </AppContext.Provider>
    )

}
export const useAppContext = () => {
    const context = useContext(AppContext);
    if (!context) {
        throw new Error('useAppContext must be used within AppProvider');
    }
    return context;
};