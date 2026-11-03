import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { ChartTooltipCard } from './ChartTooltip'
import { TrendTooltip } from './TrendChart'
import { RiskTooltip } from './RiskHistoryChart'
import { downloadText } from '../../lib/csv'
import { snapshots } from '../../test/fixtures'

type AnyProps = Parameters<typeof TrendTooltip>[0]

describe('chart tooltips', () => {
  it('renders a tooltip card with a row per series', () => {
    render(<ChartTooltipCard title="Jan 2" rows={[{ label: 'Critical', value: 1200, color: 'red' }]} footer="footer" />)
    expect(screen.getByText('1,200')).toBeInTheDocument()
    expect(screen.getByText('footer')).toBeInTheDocument()
  })

  it('trend tooltip lists every severity and the total', () => {
    const point = { date: '2026-01-02', critical: 2, high: 3, medium: 4, low: 1 }
    render(<TrendTooltip {...({ active: true, label: '2026-01-02', payload: [{ payload: point }] } as unknown as AnyProps)} />)
    expect(screen.getByText('10 open vulnerabilities')).toBeInTheDocument()
    expect(screen.getByText('Medium').nextSibling).toHaveTextContent('4')
  })

  it('risk tooltip shows score and findings', () => {
    render(<RiskTooltip {...({ active: true, payload: [{ payload: snapshots[1] }] } as unknown as AnyProps)} />)
    expect(screen.getByText('96.4')).toBeInTheDocument()
    expect(screen.getByText('3')).toBeInTheDocument()
  })

  it('tooltips render nothing when inactive', () => {
    const { container } = render(
      <>
        <TrendTooltip {...({ active: false, payload: [] } as unknown as AnyProps)} />
        <RiskTooltip {...({ active: false, payload: [] } as unknown as AnyProps)} />
      </>,
    )
    expect(container).toBeEmptyDOMElement()
  })
})

describe('downloadText', () => {
  it('creates and clicks a temporary download link', () => {
    const createObjectURL = vi.fn(() => 'blob:x')
    const revokeObjectURL = vi.fn()
    Object.assign(URL, { createObjectURL, revokeObjectURL })
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})

    downloadText('report.csv', 'a,b')

    expect(createObjectURL).toHaveBeenCalledOnce()
    expect(click).toHaveBeenCalledOnce()
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:x')
  })
})
