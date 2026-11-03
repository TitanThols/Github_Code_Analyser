import { beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route } from 'react-router-dom'
import { renderWithProviders } from '../test/render'
import { dashboard, dependencies, repository, snapshots, user, vulnerabilities } from '../test/fixtures'
import { ApiError } from '../lib/api'
import { authApi, dashboardApi, repositoriesApi } from '../api/endpoints'
import { LoginPage } from './LoginPage'
import { DashboardPage } from './DashboardPage'
import { ScanPage } from './ScanPage'
import { RepositoriesPage } from './RepositoriesPage'
import { RepositoryDetailPage } from './RepositoryDetailPage'
import { NotFoundPage } from './NotFoundPage'
import App from '../App'

vi.mock('../hooks/useLiveAlerts', () => ({ useLiveAlerts: () => 'live' }))

vi.mock('../api/endpoints', () => ({
  authApi: { me: vi.fn(), login: vi.fn(), register: vi.fn(), logout: vi.fn() },
  dashboardApi: { get: vi.fn() },
  repositoriesApi: {
    list: vi.fn(), get: vi.fn(), create: vi.fn(), rescan: vi.fn(), remove: vi.fn(),
    dependencies: vi.fn(), vulnerabilities: vi.fn(), snapshots: vi.fn(),
    sbomUrl: (id: string) => `/api/repositories/${id}/sbom`,
  },
}))

const validationError = (field: string, message: string) => new ApiError(400, { errors: { [field]: [message] } })

beforeEach(() => {
  vi.mocked(authApi.me).mockResolvedValue(user)
})

describe('LoginPage', () => {
  it('signs in with email and password', async () => {
    vi.mocked(authApi.me).mockRejectedValue(new ApiError(401, null))
    vi.mocked(authApi.login).mockResolvedValue(user)
    const u = userEvent.setup()
    renderWithProviders(<LoginPage />, { withAuth: true })

    await u.type(screen.getByLabelText('Email'), ' ada@example.com ')
    await u.type(screen.getByLabelText('Password'), 'correct-horse-battery')
    await u.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(authApi.login).toHaveBeenCalledWith('ada@example.com', 'correct-horse-battery')
  })

  it('registers and surfaces server validation errors', async () => {
    vi.mocked(authApi.me).mockRejectedValue(new ApiError(401, null))
    vi.mocked(authApi.register).mockRejectedValue(new ApiError(409, { title: 'An account with this email already exists.' }))
    const u = userEvent.setup()
    renderWithProviders(<LoginPage />, { withAuth: true })

    await u.click(screen.getByRole('tab', { name: 'Create account' }))
    await u.type(screen.getByLabelText('Name'), 'Ada')
    await u.type(screen.getByLabelText('Email'), 'ada@example.com')
    await u.type(screen.getByLabelText('Password'), 'correct-horse-battery')
    await u.click(screen.getByRole('button', { name: 'Create account' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('An account with this email already exists.')
    expect(authApi.register).toHaveBeenCalledWith('Ada', 'ada@example.com', 'correct-horse-battery')
  })
})

describe('App routing', () => {
  it('shows the sign-in page to anonymous visitors', async () => {
    vi.mocked(authApi.me).mockRejectedValue(new ApiError(401, null))
    renderWithProviders(<App />, { withAuth: true })
    expect(await screen.findByRole('heading', { name: 'Welcome back' })).toBeInTheDocument()
  })

  it('shows the dashboard to signed-in users and signs out', async () => {
    vi.mocked(dashboardApi.get).mockResolvedValue(dashboard())
    vi.mocked(authApi.logout).mockResolvedValue(undefined)
    renderWithProviders(<App />, { withAuth: true })

    expect(await screen.findByRole('heading', { name: 'Welcome back, Ada' })).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Sign out' }))
    expect(await screen.findByRole('heading', { name: 'Welcome back' })).toBeInTheDocument()
  })

  it('redirects legacy repository links and renders a 404 for unknown pages', async () => {
    vi.mocked(repositoriesApi.get).mockResolvedValue(repository({ status: 'Pending' }))
    renderWithProviders(<App />, { withAuth: true, route: '/repository/r1' })
    expect(await screen.findByText('Scan in progress')).toBeInTheDocument()
  })

  it('renders the not-found page', () => {
    renderWithProviders(<NotFoundPage />)
    expect(screen.getByRole('heading', { name: "This page doesn't exist" })).toBeInTheDocument()
  })
})

describe('DashboardPage', () => {
  it('summarises the portfolio from the API', async () => {
    vi.mocked(dashboardApi.get).mockResolvedValue(dashboard())
    renderWithProviders(<DashboardPage />, { withAuth: true })

    expect(await screen.findByText('Open vulnerabilities')).toBeInTheDocument()
    expect(screen.getByText('5 critical or high')).toBeInTheDocument()
    expect(screen.getByText('1.5K')).toBeInTheDocument()
    expect(screen.getByText('75% resolvable by upgrading')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /acme\/shop/ })).toHaveAttribute('href', '/repositories/r1')
    expect(screen.getByRole('cell', { name: /lodash/ })).toBeInTheDocument()
    expect(screen.getByLabelText('Vulnerability trend over 2 days')).toBeInTheDocument()
  })

  it('onboards a new user with the scan form', async () => {
    vi.mocked(dashboardApi.get).mockResolvedValue(dashboard({ totalRepositories: 0 }))
    renderWithProviders(<DashboardPage />, { withAuth: true })
    expect(await screen.findByRole('heading', { name: 'Scan your first repository' })).toBeInTheDocument()
    expect(screen.getByLabelText('GitHub repository')).toHaveFocus()
  })

  it('explains that the trend needs more than one day', async () => {
    vi.mocked(dashboardApi.get).mockResolvedValue(dashboard({ trend: [] }))
    renderWithProviders(<DashboardPage />, { withAuth: true })
    expect(await screen.findByText('Trend appears after the next daily re-scan')).toBeInTheDocument()
  })

  it('offers a retry when loading fails', async () => {
    vi.mocked(dashboardApi.get).mockRejectedValueOnce(new ApiError(503, null)).mockResolvedValue(dashboard())
    renderWithProviders(<DashboardPage />, { withAuth: true })
    await userEvent.click(await screen.findByRole('button', { name: /try again/i }))
    expect(await screen.findByText('Open vulnerabilities')).toBeInTheDocument()
  })
})

describe('ScanPage', () => {
  it('validates input client-side before calling the API', async () => {
    const u = userEvent.setup()
    renderWithProviders(<ScanPage />, { path: '/scan', route: '/scan' })
    await u.type(screen.getByLabelText('GitHub repository'), 'https://gitlab.com/a/b')
    await u.click(screen.getByRole('button', { name: /scan repository/i }))
    expect(screen.getByRole('alert')).toHaveTextContent(/Enter a GitHub repository/)
    expect(repositoriesApi.create).not.toHaveBeenCalled()
  })

  it('starts a scan and navigates to the repository', async () => {
    vi.mocked(repositoriesApi.create).mockResolvedValue(repository({ id: 'new-id', status: 'Pending' }))
    const u = userEvent.setup()
    renderWithProviders(<ScanPage />, { path: '/scan', route: '/scan', extraRoutes: <Route path="/repositories/:id" element={<p>Repository page</p>} /> })

    await u.click(screen.getByRole('button', { name: 'pallets/flask' }))
    await u.click(screen.getByRole('button', { name: /scan repository/i }))

    expect(await screen.findByText('Repository page')).toBeInTheDocument()
    expect(repositoriesApi.create).toHaveBeenCalledWith('pallets/flask')
  })

  it('shows the server validation message', async () => {
    vi.mocked(repositoriesApi.create).mockRejectedValue(validationError('RepositoryUrl', 'Repository not allowed'))
    const u = userEvent.setup()
    renderWithProviders(<ScanPage />, { path: '/scan', route: '/scan' })
    await u.type(screen.getByLabelText('GitHub repository'), 'acme/shop')
    await u.click(screen.getByRole('button', { name: /scan repository/i }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Repository not allowed')
  })
})

describe('RepositoriesPage', () => {
  it('lists repositories with grade, status and summary', async () => {
    vi.mocked(repositoriesApi.list).mockResolvedValue([
      repository(),
      repository({ id: 'r2', repositoryName: 'broken', status: 'Failed', errorMessage: 'No supported dependency manifests were found' }),
    ])
    renderWithProviders(<RepositoriesPage />)
    expect(await screen.findByText('1 critical · 1 high · 3 total')).toBeInTheDocument()
    expect(screen.getByRole('img', { name: /Risk grade F/ })).toBeInTheDocument()
    expect(screen.getByText(/No supported dependency manifests/)).toBeInTheDocument()
    expect(screen.getAllByText('Scanned 1 hour ago')).toHaveLength(2)
  })

  it('passes search and status filters to the API', async () => {
    vi.mocked(repositoriesApi.list).mockResolvedValue([])
    const u = userEvent.setup()
    renderWithProviders(<RepositoriesPage />)
    expect(await screen.findByText('No repositories yet')).toBeInTheDocument()

    await u.click(screen.getByRole('button', { name: 'Failed' }))
    await waitFor(() => expect(repositoriesApi.list).toHaveBeenLastCalledWith({ search: undefined, status: 'Failed' }, expect.anything()))
    await u.type(screen.getByLabelText('Filter repositories'), 'acme')
    await waitFor(() => expect(repositoriesApi.list).toHaveBeenLastCalledWith({ search: 'acme', status: 'Failed' }, expect.anything()))
    expect(screen.getByText('No repositories match')).toBeInTheDocument()
  })
})

describe('RepositoryDetailPage', () => {
  const renderDetail = () =>
    renderWithProviders(<RepositoryDetailPage />, {
      path: '/repositories/:id',
      route: '/repositories/r1',
      extraRoutes: <Route path="/repositories" element={<p>Repository list</p>} />,
    })

  beforeEach(() => {
    vi.mocked(repositoriesApi.get).mockResolvedValue(repository())
    vi.mocked(repositoriesApi.vulnerabilities).mockResolvedValue(vulnerabilities)
    vi.mocked(repositoriesApi.dependencies).mockResolvedValue(dependencies)
    vi.mocked(repositoriesApi.snapshots).mockResolvedValue(snapshots)
  })

  it('shows the report for a completed scan', async () => {
    renderDetail()
    expect(await screen.findByRole('heading', { name: /acme\/shop/ })).toBeInTheDocument()
    expect(screen.getByText('Grade F · Critical risk')).toBeInTheDocument()
    expect(await screen.findByText('CVE-2019-10744')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /SBOM/ })).toHaveAttribute('href', '/api/repositories/r1/sbom')
    expect(screen.getByLabelText('Risk score across 2 scans')).toBeInTheDocument()
    expect(screen.getByText('Fix first')).toBeInTheDocument()
  })

  it('switches to the dependencies tab', async () => {
    renderDetail()
    await userEvent.click(await screen.findByRole('tab', { name: /Dependencies/ }))
    expect(await screen.findByText('local-lib')).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: /Dependencies/ })).toHaveAttribute('aria-selected', 'true')
  })

  it('shows progress while scanning without loading findings', async () => {
    vi.mocked(repositoriesApi.get).mockResolvedValue(repository({ status: 'Parsing' }))
    renderDetail()
    expect(await screen.findByText('Scan in progress')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Scanning/ })).toBeDisabled()
    expect(repositoriesApi.vulnerabilities).not.toHaveBeenCalled()
  })

  it('explains failures and allows a re-scan', async () => {
    vi.mocked(repositoriesApi.get).mockResolvedValue(repository({ status: 'Failed', errorMessage: 'Repository was not found on GitHub' }))
    vi.mocked(repositoriesApi.rescan).mockResolvedValue(repository({ status: 'Pending' }))
    renderDetail()
    expect(await screen.findByRole('alert')).toHaveTextContent('Repository was not found on GitHub')
    await userEvent.click(screen.getByRole('button', { name: 'Re-scan' }))
    expect(repositoriesApi.rescan).toHaveBeenCalledWith('r1')
    expect(await screen.findByText('Re-scan started')).toBeInTheDocument()
  })

  it('deletes after confirmation and returns to the list', async () => {
    vi.mocked(repositoriesApi.remove).mockResolvedValue(undefined)
    const u = userEvent.setup()
    renderDetail()
    await u.click(await screen.findByRole('button', { name: 'Delete repository' }))
    await u.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Delete repository' }))
    expect(await screen.findByText('Repository list')).toBeInTheDocument()
    expect(repositoriesApi.remove).toHaveBeenCalledWith('r1')
  })

  it('renders not found for missing repositories', async () => {
    vi.mocked(repositoriesApi.get).mockRejectedValue(new ApiError(404, null))
    renderDetail()
    expect(await screen.findByRole('heading', { name: "This page doesn't exist" })).toBeInTheDocument()
  })
})
