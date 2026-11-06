import { Link } from 'react-router-dom'
import { buttonClasses } from '../components/ui/buttonStyles'

export function NotFoundPage() {
  return (
    <div className="mx-auto max-w-md py-20 text-center">
      <p className="font-mono text-sm text-accent">404</p>
      <h1 className="mt-2 text-2xl font-semibold text-fg">This page doesn't exist</h1>
      <p className="mt-2 text-fg-muted">The link may be broken, or the repository may have been deleted.</p>
      <Link to="/" className={buttonClasses('secondary', 'md', 'mt-6')}>Back to dashboard</Link>
    </div>
  )
}
