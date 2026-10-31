import { useState, type FormEvent } from 'react'
import { Activity, Boxes, Flame, LockKeyhole, Mail, UserRound } from 'lucide-react'
import { useAuth } from '../context/AuthContext'
import { ApiError } from '../lib/api'
import { cn } from '../lib/cn'
import { Button } from '../components/ui/Button'
import { Logo } from '../components/layout/AppShell'

type Mode = 'sign-in' | 'register'

const highlights = [
  { icon: Boxes, title: '8 ecosystems, 12 manifest formats', body: 'npm, PyPI, Maven/Gradle, NuGet, RubyGems, Go, Packagist and crates.io — across every package in a monorepo.' },
  { icon: Flame, title: 'Prioritised by real-world exploitation', body: 'Findings are matched by exact version against OSV.dev and flagged when they appear in the CISA KEV catalog.' },
  { icon: Activity, title: 'Continuous monitoring', body: 'Repositories are re-scanned daily and you get a live alert the moment a new advisory affects you.' },
]

function Field({ label, icon: Icon, ...props }: { label: string; icon: typeof Mail } & React.InputHTMLAttributes<HTMLInputElement>) {
  return (
    <label className="block">
      <span className="mb-1.5 block text-sm font-medium text-fg">{label}</span>
      <span className="flex h-11 items-center gap-2.5 rounded-lg border border-border bg-surface px-3 transition-colors focus-within:border-accent">
        <Icon className="size-4 text-fg-subtle" aria-hidden />
        <input className="h-full w-full min-w-0 bg-transparent text-sm text-fg outline-none placeholder:text-fg-subtle" {...props} />
      </span>
    </label>
  )
}

export function LoginPage() {
  const { signIn, register } = useAuth()
  const [mode, setMode] = useState<Mode>('sign-in')
  const [displayName, setDisplayName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      if (mode === 'register') await register(displayName.trim(), email.trim(), password)
      else await signIn(email.trim(), password)
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Something went wrong. Please try again.')
    } finally {
      setSubmitting(false)
    }
  }

  const switchMode = (next: Mode) => {
    setMode(next)
    setError(null)
  }

  return (
    <div className="grid min-h-dvh lg:grid-cols-[1.1fr_1fr]">
      <section className="relative hidden overflow-hidden border-r border-border bg-surface lg:flex lg:flex-col lg:justify-between lg:p-12">
        <div
          className="pointer-events-none absolute inset-0 opacity-[0.5]"
          style={{ backgroundImage: 'radial-gradient(circle at 1px 1px, var(--border) 1px, transparent 0)', backgroundSize: '24px 24px' }}
          aria-hidden
        />
        <Logo className="relative text-lg" />
        <div className="relative max-w-xl">
          <h1 className="text-4xl font-semibold leading-tight tracking-tight text-fg xl:text-5xl">
            Find vulnerable dependencies before attackers do.
          </h1>
          <p className="mt-4 text-lg text-fg-muted">
            Point DepScan at any public GitHub repository. Get a prioritised, version-accurate list of what to fix — and an SBOM to prove it.
          </p>
          <ul className="mt-10 space-y-6">
            {highlights.map(({ icon: Icon, title, body }) => (
              <li key={title} className="flex gap-4">
                <span className="grid size-10 shrink-0 place-items-center rounded-xl border border-border bg-surface-2 text-accent">
                  <Icon className="size-5" aria-hidden />
                </span>
                <div>
                  <p className="font-medium text-fg">{title}</p>
                  <p className="mt-0.5 text-sm text-fg-muted">{body}</p>
                </div>
              </li>
            ))}
          </ul>
        </div>
        <p className="relative text-xs text-fg-subtle">Advisory data: OSV.dev · CISA Known Exploited Vulnerabilities</p>
      </section>

      <section className="flex items-center justify-center px-4 py-12 sm:px-8">
        <div className="w-full max-w-sm">
          <Logo className="mb-10 text-lg lg:hidden" />
          <h2 className="text-2xl font-semibold tracking-tight text-fg">{mode === 'sign-in' ? 'Welcome back' : 'Create your account'}</h2>
          <p className="mt-1 text-sm text-fg-muted">
            {mode === 'sign-in' ? 'Sign in to view your repositories and alerts.' : 'Free, and it takes ten seconds.'}
          </p>

          <div className="mt-6 grid grid-cols-2 rounded-lg bg-surface-2 p-1" role="tablist" aria-label="Account">
            {(['sign-in', 'register'] as const).map((m) => (
              <button
                key={m}
                type="button"
                role="tab"
                aria-selected={mode === m}
                onClick={() => switchMode(m)}
                className={cn('h-8 rounded-md text-sm font-medium transition-colors', mode === m ? 'bg-surface text-fg shadow-card' : 'text-fg-muted hover:text-fg')}
              >
                {m === 'sign-in' ? 'Sign in' : 'Create account'}
              </button>
            ))}
          </div>

          <form onSubmit={submit} className="mt-6 space-y-4">
            {mode === 'register' && (
              <Field label="Name" icon={UserRound} autoComplete="name" value={displayName} onChange={(e) => setDisplayName(e.target.value)} minLength={2} maxLength={80} required placeholder="Ada Lovelace" />
            )}
            <Field label="Email" icon={Mail} type="email" autoComplete="email" value={email} onChange={(e) => setEmail(e.target.value)} maxLength={254} required placeholder="you@company.com" />
            <Field
              label="Password"
              icon={LockKeyhole}
              type="password"
              autoComplete={mode === 'register' ? 'new-password' : 'current-password'}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              minLength={mode === 'register' ? 12 : 1}
              maxLength={128}
              required
              placeholder={mode === 'register' ? 'At least 12 characters' : '••••••••••••'}
            />
            {error && (
              <p role="alert" className="rounded-lg border border-danger/30 bg-danger/10 px-3 py-2 text-sm text-fg">
                {error}
              </p>
            )}
            <Button type="submit" loading={submitting} className="w-full" size="lg">
              {mode === 'sign-in' ? 'Sign in' : 'Create account'}
            </Button>
          </form>
        </div>
      </section>
    </div>
  )
}
