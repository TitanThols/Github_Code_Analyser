import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { authApi } from '../api/endpoints'
import { ApiError } from '../lib/api'
import type { User } from '../types'

interface AuthContextValue {
  status: 'loading' | 'authenticated' | 'anonymous'
  user: User | null
  signIn: (email: string, password: string) => Promise<void>
  register: (displayName: string, email: string, password: string) => Promise<void>
  signOut: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null)
  const [status, setStatus] = useState<AuthContextValue['status']>('loading')
  const queryClient = useQueryClient()

  useEffect(() => {
    const controller = new AbortController()
    authApi
      .me(controller.signal)
      .then((me) => {
        setUser(me)
        setStatus('authenticated')
      })
      .catch((error: unknown) => {
        if (error instanceof DOMException && error.name === 'AbortError') return
        if (!(error instanceof ApiError) || error.status !== 401) console.warn('Session check failed', error)
        setStatus('anonymous')
      })
    return () => controller.abort()
  }, [])

  const signIn = useCallback(async (email: string, password: string) => {
    setUser(await authApi.login(email, password))
    setStatus('authenticated')
  }, [])

  const register = useCallback(async (displayName: string, email: string, password: string) => {
    setUser(await authApi.register(displayName, email, password))
    setStatus('authenticated')
  }, [])

  const signOut = useCallback(async () => {
    try {
      await authApi.logout()
    } finally {
      queryClient.clear() // never show the previous user's data to the next one
      setUser(null)
      setStatus('anonymous')
    }
  }, [queryClient])

  const value = useMemo(() => ({ status, user, signIn, register, signOut }), [status, user, signIn, register, signOut])
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth must be used within AuthProvider')
  return context
}
