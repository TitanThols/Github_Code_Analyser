import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { analyzeRepository } from '../api/repositories';

const AnalyzePage = () => {
    const [url, setUrl] = useState('');
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const navigate = useNavigate();

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault()

        if (!url.trim()) return;

        try {
            setIsLoading(true);
            setError(null);

            const result = await analyzeRepository(url);
            navigate(`/repository/${result.id}`);
        } catch (err) {
            setError('Failed to analyze repository');
        } finally {
            setIsLoading(false);
        }

    };
    return (
        <div className="analyze-page">
            <h2>Analyze a github repository</h2>
            <p>Enter the github repo URL to start the analysis</p>
            <form onSubmit={handleSubmit} className='analyze-form'>
                <input
                    type="text"
                    value={url}
                    onChange={(e) => setUrl(e.target.value)}
                    placeholder="https://github.com/user/repo"
                    disabled={isLoading}
                    required
                />
                <button type="submit" disabled={isLoading}>
                    {isLoading ? 'Analyzing...' : 'Analyze'}
                </button>
            </form>
            {error && <p className="error-message">{error}</p>}
        </div>
    );

};

export default AnalyzePage;
