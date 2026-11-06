import { createContext, useCallback, useContext, useMemo, useRef, useState, type ReactNode } from 'react'
import { AlertTriangle, CheckCircle2, Info, X, XCircle } from 'lucide-react'
import { Link } from 'react-router-dom'
import { cn } from '../lib/cn'

export type ToastTone = 'info' | 'success' | 'warning' | 'error'

export interface ToastInput {
  title: string
  description?: string
  tone?: ToastTone
  href?: string
  durationMs?: number
}

interface Toast extends ToastInput {
  id: number
}

const ToastContext = createContext<((toast: ToastInput) => void) | undefined>(undefined)

const icons = { info: Info, success: CheckCircle2, warning: AlertTriangle, error: XCircle }
const accents = { info: 'text-accent', success: 'text-good', warning: 'text-sev-medium', error: 'text-danger' }

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([])
  const nextId = useRef(1)

  const dismiss = useCallback((id: number) => setToasts((current) => current.filter((t) => t.id !== id)), [])

  const push = useCallback(
    (toast: ToastInput) => {
      const id = nextId.current++
      setToasts((current) => [...current.slice(-3), { ...toast, id }])
      window.setTimeout(() => dismiss(id), toast.durationMs ?? 6000)
    },
    [dismiss],
  )

  const value = useMemo(() => push, [push])

  return (
    <ToastContext.Provider value={value}>
      {children}
      <div aria-live="polite" className="pointer-events-none fixed inset-x-4 bottom-4 z-50 flex flex-col items-end gap-2 sm:left-auto sm:w-96">
        {toasts.map((toast) => {
          const Icon = icons[toast.tone ?? 'info']
          const body = (
            <>
              <Icon className={cn('mt-0.5 size-5 shrink-0', accents[toast.tone ?? 'info'])} aria-hidden />
              <div className="min-w-0 flex-1">
                <p className="text-sm font-semibold text-fg">{toast.title}</p>
                {toast.description && <p className="mt-0.5 text-sm text-fg-muted">{toast.description}</p>}
              </div>
            </>
          )
          return (
            <div
              key={toast.id}
              role="status"
              className="pointer-events-auto flex w-full items-start gap-3 rounded-xl border border-border bg-surface p-4 shadow-pop"
            >
              {toast.href ? (
                <Link to={toast.href} className="flex min-w-0 flex-1 items-start gap-3" onClick={() => dismiss(toast.id)}>
                  {body}
                </Link>
              ) : (
                body
              )}
              <button type="button" onClick={() => dismiss(toast.id)} className="rounded-md p-0.5 text-fg-subtle hover:text-fg" aria-label="Dismiss notification">
                <X className="size-4" />
              </button>
            </div>
          )
        })}
      </div>
    </ToastContext.Provider>
  )
}

// eslint-disable-next-line react-refresh/only-export-components
export function useToast() {
  const context = useContext(ToastContext)
  if (!context) throw new Error('useToast must be used within ToastProvider')
  return context
}
