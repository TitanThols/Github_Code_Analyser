import { FileCode2, Gauge, GitBranch, ShieldCheck } from 'lucide-react'
import { ScanForm } from '../components/scan/ScanForm'

const manifests = [
  'package.json', 'requirements*.txt', 'pyproject.toml', 'pom.xml', 'build.gradle(.kts)', '*.csproj',
  'Directory.Packages.props', 'Gemfile', 'go.mod', 'composer.json', 'Cargo.toml',
]

const steps = [
  { icon: GitBranch, title: 'Discover', body: 'Walks the repository tree and finds every manifest — including nested packages in monorepos.' },
  { icon: FileCode2, title: 'Resolve', body: 'Parses each manifest and pins every version constraint to the oldest version it allows.' },
  { icon: ShieldCheck, title: 'Match', body: 'Checks each package version against OSV.dev and flags CVEs exploited in the wild (CISA KEV).' },
  { icon: Gauge, title: 'Prioritise', body: 'Scores findings from CVSS, severity and exploitation, then grades the repository A–F.' },
]

export function ScanPage() {
  return (
    <div className="mx-auto max-w-3xl py-4 sm:py-10">
      <p className="text-sm font-medium text-accent">New scan</p>
      <h1 className="mt-2 text-3xl font-semibold tracking-tight text-fg sm:text-4xl">What should we scan?</h1>
      <p className="mt-3 text-fg-muted">Any public GitHub repository. Most scans finish in under a minute; you'll get a live notification when it's done.</p>

      <div className="mt-8">
        <ScanForm autoFocus />
      </div>

      <section className="mt-14" aria-labelledby="how-title">
        <h2 id="how-title" className="text-sm font-semibold uppercase tracking-wide text-fg-subtle">How a scan works</h2>
        <ol className="mt-4 grid gap-4 sm:grid-cols-2">
          {steps.map(({ icon: Icon, title, body }, i) => (
            <li key={title} className="rounded-2xl border border-border bg-surface p-5">
              <div className="flex items-center gap-3">
                <span className="grid size-9 place-items-center rounded-lg bg-accent-soft text-accent"><Icon className="size-[18px]" aria-hidden /></span>
                <span className="font-medium text-fg">{i + 1}. {title}</span>
              </div>
              <p className="mt-3 text-sm text-fg-muted">{body}</p>
            </li>
          ))}
        </ol>
      </section>

      <section className="mt-10" aria-labelledby="manifests-title">
        <h2 id="manifests-title" className="text-sm font-semibold uppercase tracking-wide text-fg-subtle">Supported manifests</h2>
        <ul className="mt-4 flex flex-wrap gap-2">
          {manifests.map((m) => (
            <li key={m} className="rounded-lg border border-border bg-surface px-2.5 py-1 font-mono text-xs text-fg-muted">{m}</li>
          ))}
        </ul>
      </section>
    </div>
  )
}
