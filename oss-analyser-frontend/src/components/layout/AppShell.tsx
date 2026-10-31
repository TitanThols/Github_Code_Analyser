import { NavLink, Outlet, Link } from 'react-router-dom'
import { FolderGit2, LayoutDashboard, LogOut, Moon, Plus, ShieldCheck, Sun } from 'lucide-react'
import { useAuth } from '../../context/AuthContext'
import { useTheme } from '../../hooks/useTheme'
import { useLiveAlerts, type ConnectionState } from '../../hooks/useLiveAlerts'
import { cn } from '../../lib/cn'
import { buttonClasses } from '../ui/buttonStyles'

const navItems = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard, end: true },
  { to: '/repositories', label: 'Repositories', icon: FolderGit2, end: false },
]

const connectionCopy: Record<ConnectionState, string> = {
  live: 'Live updates on',
  connecting: 'Connecting…',
  offline: 'Live updates paused',
}

export function Logo({ className }: { className?: string }) {
  return (
    <span className={cn('inline-flex items-center gap-2 font-semibold tracking-tight text-fg', className)}>
      <span className="grid size-8 place-items-center rounded-lg bg-accent text-accent-fg">
        <ShieldCheck className="size-[18px]" strokeWidth={2.4} aria-hidden />
      </span>
      DepScan
    </span>
  )
}

export function AppShell() {
  const { user, signOut } = useAuth()
  const { theme, toggle } = useTheme()
  const connection = useLiveAlerts(Boolean(user))

  return (
    <div className="flex min-h-dvh flex-col">
      <a href="#main" className="sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-4 focus:z-50 focus:rounded-lg focus:bg-surface focus:px-3 focus:py-2">
        Skip to content
      </a>
      <header className="sticky top-0 z-40 border-b border-border bg-bg/85 backdrop-blur-md">
        <div className="mx-auto flex h-16 max-w-7xl items-center gap-4 px-4 sm:px-6">
          <Link to="/" aria-label="DepScan home">
            <Logo />
          </Link>

          <nav aria-label="Main" className="ml-4 hidden items-center gap-1 md:flex">
            {navItems.map(({ to, label, icon: Icon, end }) => (
              <NavLink
                key={to}
                to={to}
                end={end}
                className={({ isActive }) =>
                  cn('inline-flex items-center gap-2 rounded-lg px-3 py-2 text-sm font-medium transition-colors', isActive ? 'bg-surface-2 text-fg' : 'text-fg-muted hover:text-fg')
                }
              >
                <Icon className="size-4" aria-hidden />
                {label}
              </NavLink>
            ))}
          </nav>

          <div className="ml-auto flex items-center gap-2">
            <span className="hidden items-center gap-2 text-xs text-fg-subtle lg:inline-flex" role="status">
              <span
                className={cn('size-2 rounded-full', connection === 'live' ? 'bg-good animate-[pulse-ring_2s_infinite]' : connection === 'connecting' ? 'bg-sev-medium' : 'bg-fg-subtle')}
                aria-hidden
              />
              {connectionCopy[connection]}
            </span>
            <Link to="/scan" className={buttonClasses('primary', 'sm', 'hidden sm:inline-flex')}>
              <Plus className="size-4" aria-hidden />
              New scan
            </Link>
            <button
              type="button"
              onClick={toggle}
              className="grid size-9 place-items-center rounded-lg text-fg-muted transition-colors hover:bg-surface-2 hover:text-fg"
              aria-label={theme === 'dark' ? 'Switch to light theme' : 'Switch to dark theme'}
            >
              {theme === 'dark' ? <Sun className="size-[18px]" /> : <Moon className="size-[18px]" />}
            </button>
            {user && (
              <div className="flex items-center gap-2 border-l border-border pl-3">
                <span className="grid size-8 place-items-center rounded-full bg-accent-soft text-sm font-semibold text-accent" aria-hidden>
                  {user.displayName.slice(0, 1).toUpperCase()}
                </span>
                <span className="hidden max-w-32 truncate text-sm font-medium text-fg xl:block">{user.displayName}</span>
                <button
                  type="button"
                  onClick={() => void signOut()}
                  className="grid size-9 place-items-center rounded-lg text-fg-muted transition-colors hover:bg-surface-2 hover:text-fg"
                  aria-label="Sign out"
                  title="Sign out"
                >
                  <LogOut className="size-[18px]" />
                </button>
              </div>
            )}
          </div>
        </div>

        <nav aria-label="Main mobile" className="flex gap-1 overflow-x-auto px-4 pb-2 md:hidden">
          {[...navItems, { to: '/scan', label: 'New scan', icon: Plus, end: false }].map(({ to, label, icon: Icon, end }) => (
            <NavLink
              key={to}
              to={to}
              end={end}
              className={({ isActive }) =>
                cn('inline-flex shrink-0 items-center gap-1.5 rounded-lg px-3 py-1.5 text-sm font-medium', isActive ? 'bg-surface-2 text-fg' : 'text-fg-muted')
              }
            >
              <Icon className="size-4" aria-hidden />
              {label}
            </NavLink>
          ))}
        </nav>
      </header>

      <main id="main" className="mx-auto w-full max-w-7xl flex-1 px-4 py-6 sm:px-6 sm:py-8">
        <Outlet />
      </main>

      <footer className="border-t border-border">
        <div className="mx-auto flex max-w-7xl flex-wrap items-center justify-between gap-2 px-4 py-5 text-xs text-fg-subtle sm:px-6">
          <span>DepScan · open-source dependency risk intelligence</span>
          <span>
            Advisory data from{' '}
            <a className="underline-offset-2 hover:text-fg hover:underline" href="https://osv.dev" target="_blank" rel="noreferrer">OSV.dev</a> and the{' '}
            <a className="underline-offset-2 hover:text-fg hover:underline" href="https://www.cisa.gov/known-exploited-vulnerabilities-catalog" target="_blank" rel="noreferrer">CISA KEV catalog</a>
          </span>
        </div>
      </footer>
    </div>
  )
}
