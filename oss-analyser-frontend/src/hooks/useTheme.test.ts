import { act, renderHook } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { useTheme } from './useTheme'

describe('useTheme', () => {
  it('follows the system preference by default', () => {
    expect(renderHook(() => useTheme()).result.current.theme).toBe('light')
  })

  it('restores a saved choice', () => {
    localStorage.setItem('depscan.theme', 'dark')
    expect(renderHook(() => useTheme()).result.current.theme).toBe('dark')
  })

  it('toggles, applies and persists the theme', () => {
    const { result } = renderHook(() => useTheme())
    act(() => result.current.toggle())
    expect(result.current.theme).toBe('dark')
    expect(document.documentElement.dataset.theme).toBe('dark')
    expect(localStorage.getItem('depscan.theme')).toBe('dark')
    act(() => result.current.toggle())
    expect(document.documentElement.dataset.theme).toBe('light')
  })
})
