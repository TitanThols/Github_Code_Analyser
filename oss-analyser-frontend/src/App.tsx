import { lazy, Suspense } from 'react'
import { Navigate, Route, Routes, useParams } from 'react-router-dom'
import { useAuth } from './context/AuthContext'
import { AppShell } from './components/layout/AppShell'
import { LoginPage } from './pages/LoginPage'
import { NotFoundPage } from './pages/NotFoundPage'
import { Skeleton } from './components/ui/Feedback'

// Route-level code splitting keeps the charting library off the sign-in page.
const DashboardPage = lazy(() => import('./pages/DashboardPage').then((m) => ({ default: m.DashboardPage })))
const ScanPage = lazy(() => import('./pages/ScanPage').then((m) => ({ default: m.ScanPage })))
const RepositoriesPage = lazy(() => import('./pages/RepositoriesPage').then((m) => ({ default: m.RepositoriesPage })))
const RepositoryDetailPage = lazy(() => import('./pages/RepositoryDetailPage').then((m) => ({ default: m.RepositoryDetailPage })))

function LegacyRepositoryRedirect() {
  const { id } = useParams()
  return <Navigate to={`/repositories/${id}`} replace />
}

const PageFallback = () => (
  <div className="space-y-4" aria-busy="true">
    <Skeleton className="h-9 w-64" />
    <Skeleton className="h-64 rounded-2xl" />
  </div>
)

export default function App() {
  const { status } = useAuth()

  if (status === 'loading') {
    return (
      <div className="grid min-h-dvh place-items-center" role="status" aria-label="Checking your session">
        <span className="size-8 animate-spin rounded-full border-2 border-border border-t-accent" />
      </div>
    )
  }

  if (status === 'anonymous') return <LoginPage />

  return (
    <Routes>
      <Route element={<AppShell />}>
        <Route index element={<Suspense fallback={<PageFallback />}><DashboardPage /></Suspense>} />
        <Route path="scan" element={<Suspense fallback={<PageFallback />}><ScanPage /></Suspense>} />
        <Route path="repositories" element={<Suspense fallback={<PageFallback />}><RepositoriesPage /></Suspense>} />
        <Route path="repositories/:id" element={<Suspense fallback={<PageFallback />}><RepositoryDetailPage /></Suspense>} />
        <Route path="repository/:id" element={<LegacyRepositoryRedirect />} />
        <Route path="analyze" element={<Navigate to="/scan" replace />} />
        <Route path="*" element={<NotFoundPage />} />
      </Route>
    </Routes>
  )
}
