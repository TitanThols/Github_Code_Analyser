import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { ArrowRight, FolderGit2 } from 'lucide-react'
import { useCreateScan } from '../../hooks/queries'
import { ApiError } from '../../lib/api'
import { parseRepositoryInput } from '../../lib/github'
import { cn } from '../../lib/cn'
import { EXAMPLE_REPOSITORIES } from '../../lib/github'
import { Button } from '../ui/Button'

export function ScanForm({ autoFocus = false, size = 'lg' }: { autoFocus?: boolean; size?: 'md' | 'lg' }) {
  const [value, setValue] = useState('')
  const [touched, setTouched] = useState(false)
  const navigate = useNavigate()
  const createScan = useCreateScan()

  const parsed = parseRepositoryInput(value)
  const clientError = touched && value.trim() && !parsed ? 'Enter a GitHub repository like owner/name or a github.com URL.' : null
  const serverError = createScan.error instanceof ApiError ? (createScan.error.fieldError('repositoryUrl') ?? createScan.error.message) : createScan.error?.message
  const error = clientError ?? serverError ?? null

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    setTouched(true)
    if (!parsed) return
    const repository = await createScan.mutateAsync(value.trim()).catch(() => null)
    if (repository) navigate(`/repositories/${repository.id}`)
  }

  return (
    <div>
      <form onSubmit={submit} noValidate className="flex flex-col gap-2 sm:flex-row">
        <label htmlFor="repository-url" className="sr-only">GitHub repository</label>
        <div
          className={cn(
            'flex flex-1 items-center gap-3 rounded-xl border bg-surface px-4 shadow-card transition-colors focus-within:border-accent',
            error ? 'border-danger' : 'border-border',
            size === 'lg' ? 'h-14' : 'h-11',
          )}
        >
          <FolderGit2 className="size-5 shrink-0 text-fg-subtle" aria-hidden />
          <input
            id="repository-url"
            value={value}
            onChange={(e) => {
              setValue(e.target.value)
              createScan.reset()
            }}
            onBlur={() => setTouched(true)}
            placeholder="github.com/owner/repository"
            autoComplete="off"
            spellCheck={false}
            autoFocus={autoFocus}
            aria-invalid={Boolean(error)}
            aria-describedby={error ? 'repository-url-error' : undefined}
            className="h-full w-full min-w-0 bg-transparent text-base text-fg outline-none placeholder:text-fg-subtle"
          />
        </div>
        <Button type="submit" size={size} loading={createScan.isPending} className={size === 'lg' ? 'h-14 px-6' : ''}>
          {createScan.isPending ? 'Starting scan' : 'Scan repository'}
          {!createScan.isPending && <ArrowRight className="size-4" aria-hidden />}
        </Button>
      </form>
      {error && (
        <p id="repository-url-error" role="alert" className="mt-2 text-sm text-danger">
          {error}
        </p>
      )}
      <div className="mt-4 flex flex-wrap items-center gap-2 text-sm">
        <span className="text-fg-subtle">Try</span>
        {EXAMPLE_REPOSITORIES.map((example) => (
          <button
            key={example}
            type="button"
            onClick={() => {
              setValue(example)
              setTouched(false)
              createScan.reset()
            }}
            className="rounded-full border border-border bg-surface px-3 py-1 font-mono text-xs text-fg-muted transition-colors hover:border-border-strong hover:text-fg"
          >
            {example}
          </button>
        ))}
      </div>
    </div>
  )
}
