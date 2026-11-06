import { useEffect, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { apiUrl } from '../lib/api'
import { useToast } from '../context/ToastContext'
import { queryKeys } from './queries'
import type { AlertMessage } from '../types'

export type ConnectionState = 'connecting' | 'live' | 'offline'

/**
 * Subscribes to the per-user SignalR channel. Every message refreshes the affected cached
 * queries; completions, failures and newly published vulnerabilities also raise a toast.
 */
export function useLiveAlerts(enabled: boolean): ConnectionState {
  const [state, setState] = useState<ConnectionState>('connecting')
  const queryClient = useQueryClient()
  const toast = useToast()

  useEffect(() => {
    if (!enabled) return
    let disposed = false
    let stop: (() => Promise<void>) | undefined

    void (async () => {
      // Loaded lazily so the ~40 kB client isn't on the critical path.
      const signalR = await import('@microsoft/signalr')
      if (disposed) return

      const connection = new signalR.HubConnectionBuilder()
        .withUrl(apiUrl('/hubs/alerts'), { withCredentials: true })
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(signalR.LogLevel.Warning)
        .build()
      stop = () => connection.stop()

      connection.on('ReceiveAlert', (message: AlertMessage) => {
        void queryClient.invalidateQueries({ queryKey: queryKeys.repository(message.repositoryId) })
        void queryClient.invalidateQueries({ queryKey: ['repositories'] })
        if (message.type === 'ANALYSIS_PROGRESS') return
        void queryClient.invalidateQueries({ queryKey: queryKeys.dashboard })

        const href = `/repositories/${message.repositoryId}`
        if (message.type === 'ANALYSIS_COMPLETED') {
          const urgent = (message.critical ?? 0) + (message.high ?? 0)
          toast({
            tone: urgent > 0 ? 'warning' : 'success',
            title: `Scan complete · ${message.repositoryName}`,
            description: urgent > 0 ? `${message.critical} critical and ${message.high} high severity findings` : 'No critical or high severity findings',
            href,
          })
        } else if (message.type === 'ANALYSIS_FAILED') {
          toast({ tone: 'error', title: `Scan failed · ${message.repositoryName}`, description: message.message ?? undefined, href })
        } else if (message.type === 'NEW_VULNERABILITY') {
          toast({
            tone: message.severity === 'Critical' || message.severity === 'High' ? 'warning' : 'info',
            title: `New ${message.severity?.toLowerCase()} vulnerability in ${message.packageName}`,
            description: `${message.cveId} · ${message.repositoryName}`,
            href,
          })
        }
      })

      connection.onreconnecting(() => !disposed && setState('connecting'))
      connection.onreconnected(() => !disposed && setState('live'))
      connection.onclose(() => !disposed && setState('offline'))

      try {
        await connection.start()
        if (!disposed) setState('live')
      } catch {
        if (!disposed) setState('offline')
      }
    })()

    return () => {
      disposed = true
      void stop?.()
    }
  }, [enabled, queryClient, toast])

  return enabled ? state : 'offline'
}
