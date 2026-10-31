/** RFC 7807 problem details as produced by ASP.NET Core. */
export interface ProblemDetails {
  title?: string
  detail?: string
  status?: number
  errors?: Record<string, string[]>
}

export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails | null

  constructor(status: number, problem: ProblemDetails | null) {
    super(ApiError.describe(status, problem))
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }

  /** First validation message for a field (case-insensitive), e.g. fieldError('repositoryUrl'). */
  fieldError(field: string): string | undefined {
    const entry = Object.entries(this.problem?.errors ?? {}).find(([key]) => key.toLowerCase() === field.toLowerCase())
    return entry?.[1][0]
  }

  private static describe(status: number, problem: ProblemDetails | null): string {
    const firstValidationError = Object.values(problem?.errors ?? {}).flat()[0]
    if (firstValidationError) return firstValidationError
    if (problem?.detail) return problem.detail
    if (problem?.title && !problem.title.startsWith('One or more validation errors')) return problem.title
    if (status === 0) return 'Unable to reach the server. Check your connection and try again.'
    if (status === 401) return 'Your session has expired. Please sign in again.'
    if (status === 404) return 'Not found.'
    if (status === 429) return 'Too many requests. Please wait a moment and try again.'
    if (status >= 500) return 'Something went wrong on our side. Please try again.'
    return `Request failed (${status}).`
  }
}

const baseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '')

export const apiUrl = (path: string) => `${baseUrl}${path}`

type Method = 'GET' | 'POST' | 'DELETE'

export async function request<T>(method: Method, path: string, body?: unknown, signal?: AbortSignal): Promise<T> {
  let response: Response
  try {
    response = await fetch(apiUrl(path), {
      method,
      credentials: 'include',
      signal,
      headers: body === undefined ? { Accept: 'application/json' } : { Accept: 'application/json', 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw new ApiError(0, null)
  }

  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as ProblemDetails | null
    throw new ApiError(response.status, problem)
  }

  if (response.status === 204 || response.headers.get('content-length') === '0') return undefined as T
  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}

export const api = {
  get: <T>(path: string, signal?: AbortSignal) => request<T>('GET', path, undefined, signal),
  post: <T>(path: string, body?: unknown) => request<T>('POST', path, body ?? {}),
  delete: <T = void>(path: string) => request<T>('DELETE', path),
}
