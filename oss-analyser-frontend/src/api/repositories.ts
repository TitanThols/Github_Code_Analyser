import { apiClient } from './client';
import type { Repository, Dependency, Vulnerability, VulnerabilitySnapshot, DashboardStats } from '../types';

export const analyzeRepository = async (repositoryUrl: string): Promise<Repository> => {
    const res = await apiClient.post<Repository>('/analyze', { repositoryUrl });
    return res.data;
};

export const listRepositories = async (): Promise<Repository[]> => {
    const res = await apiClient.get<Repository[]>('/analyze');
    return res.data;
};

export const getRepository = async (id: string): Promise<Repository> => {
    const res = await apiClient.get<Repository>(`/analyze/${id}`);
    return res.data;
};

export const getDependencies = async (id: string): Promise<Dependency[]> => {
    const res = await apiClient.get<Dependency[]>(`/analyze/${id}/dependencies`);
    return res.data;
};

export const getVulnerabilities = async (id: string): Promise<Vulnerability[]> => {
    const res = await apiClient.get<Vulnerability[]>(`/analyze/${id}/vulnerabilities`);
    return res.data;
};

export const getSnapshots = async (id: string): Promise<VulnerabilitySnapshot[]> => {
    const res = await apiClient.get<VulnerabilitySnapshot[]>(`/analyze/${id}/snapshots`);
    return res.data;
};

export const getStats = async (): Promise<DashboardStats> => {
    const res = await apiClient.get<DashboardStats>('/analyze/stats');
    return res.data;
};
