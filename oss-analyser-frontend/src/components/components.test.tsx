import { describe, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { SeverityBar } from './charts/SeverityBar'
import { EcosystemBars } from './charts/EcosystemBars'
import { ExploitedBadge, RiskGradeBadge, SeverityBadge, StatusBadge } from './ui/Badges'
import { Button } from './ui/Button'
import { ConfirmDialog } from './ui/ConfirmDialog'
import { EmptyState, ErrorState } from './ui/Feedback'
import { ScanProgress } from './scan/ScanProgress'
import { VulnerabilityList } from './repository/VulnerabilityList'
import { DependencyTable } from './repository/DependencyTable'
import { RemediationPlan } from './repository/RemediationPlan'
import { dependencies, vulnerabilities } from '../test/fixtures'
import { ToastProvider, useToast } from '../context/ToastContext'
import { MemoryRouter } from 'react-router-dom'

describe('SeverityBar', () => {
  it('summarises counts accessibly and labels every severity', () => {
    render(<SeverityBar counts={{ Critical: 2, High: 0, Medium: 5, Low: 1 }} />)
    expect(screen.getByRole('img')).toHaveAccessibleName('2 critical, 0 high, 5 medium, 1 low')
    const legend = screen.getByRole('list')
    expect(within(legend).getByText('Medium').nextSibling).toHaveTextContent('5')
    expect(within(legend).getAllByRole('listitem')).toHaveLength(4)
  })

  it('renders an empty track when there are no findings', () => {
    render(<SeverityBar counts={{ Critical: 0, High: 0, Medium: 0, Low: 0 }} showLegend={false} />)
    expect(screen.getByRole('img')).toHaveAccessibleName('No vulnerabilities')
    expect(screen.queryByRole('list')).not.toBeInTheDocument()
  })
})

describe('EcosystemBars', () => {
  it('shows dependency and vulnerability counts per ecosystem', () => {
    render(<EcosystemBars ecosystems={[{ ecosystem: 'Gem', dependencies: 10, vulnerabilities: 2 }, { ecosystem: 'Cargo', dependencies: 4, vulnerabilities: 0 }]} />)
    expect(screen.getByText('RubyGems')).toBeInTheDocument()
    expect(screen.getByText('crates.io')).toBeInTheDocument()
    expect(screen.getByText(/2 vulns/)).toBeInTheDocument()
  })
})

describe('badges', () => {
  it('never conveys severity by colour alone', () => {
    render(<SeverityBadge severity="High" />)
    expect(screen.getByText('High')).toBeInTheDocument()
  })

  it('describes the risk grade for screen readers', () => {
    render(<RiskGradeBadge grade="F" />)
    expect(screen.getByRole('img', { name: 'Risk grade F: Critical risk' })).toBeInTheDocument()
  })

  it.each([
    ['Pending', 'Queued'],
    ['FetchingVulnerabilities', 'Checking advisories'],
    ['Failed', 'Failed'],
  ] as const)('labels %s status', (status, label) => {
    render(<StatusBadge status={status} />)
    expect(screen.getByText(label)).toBeInTheDocument()
  })

  it('explains the exploited badge', () => {
    render(<ExploitedBadge />)
    expect(screen.getByText('Exploited')).toHaveAttribute('title', expect.stringContaining('CISA'))
  })
})

describe('Button', () => {
  it('disables itself and announces busy while loading', () => {
    render(<Button loading>Save</Button>)
    const button = screen.getByRole('button', { name: 'Save' })
    expect(button).toBeDisabled()
    expect(button).toHaveAttribute('aria-busy', 'true')
  })
})

describe('feedback states', () => {
  it('renders empty and error states with actions', async () => {
    const retry = vi.fn()
    render(
      <>
        <EmptyState title="Nothing here" description="Add something" action={<button>Add</button>} />
        <ErrorState message="Boom" onRetry={retry} />
      </>,
    )
    expect(screen.getByText('Nothing here')).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent('Boom')
    await userEvent.click(screen.getByRole('button', { name: /try again/i }))
    expect(retry).toHaveBeenCalledOnce()
  })
})

describe('ConfirmDialog', () => {
  it('focuses cancel, confirms, and closes on Escape', async () => {
    const onConfirm = vi.fn()
    const onCancel = vi.fn()
    const { rerender } = render(<ConfirmDialog open title="Delete?" description="Gone forever" confirmLabel="Delete" onConfirm={onConfirm} onCancel={onCancel} />)
    expect(screen.getByRole('alertdialog', { name: 'Delete?' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Cancel' })).toHaveFocus()
    await userEvent.click(screen.getByRole('button', { name: 'Delete' }))
    expect(onConfirm).toHaveBeenCalledOnce()
    await userEvent.keyboard('{Escape}')
    expect(onCancel).toHaveBeenCalledOnce()
    rerender(<ConfirmDialog open={false} title="Delete?" description="" confirmLabel="Delete" onConfirm={onConfirm} onCancel={onCancel} />)
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
  })
})

describe('ScanProgress', () => {
  it('marks the current step', () => {
    render(<ScanProgress status="FetchingVulnerabilities" />)
    const current = screen.getAllByRole('listitem').find((li) => li.getAttribute('aria-current') === 'step')
    expect(current).toHaveTextContent('Checking advisories')
  })
})

describe('Toasts', () => {
  function Trigger() {
    const toast = useToast()
    return <button onClick={() => toast({ title: 'Scan complete', description: 'acme/shop', tone: 'success', href: '/repositories/r1' })}>notify</button>
  }

  it('shows and dismisses notifications', async () => {
    render(<MemoryRouter><ToastProvider><Trigger /></ToastProvider></MemoryRouter>)
    await userEvent.click(screen.getByText('notify'))
    expect(screen.getByRole('status')).toHaveTextContent('Scan complete')
    expect(screen.getByRole('link')).toHaveAttribute('href', '/repositories/r1')
    await userEvent.click(screen.getByRole('button', { name: 'Dismiss notification' }))
    expect(screen.queryByText('Scan complete')).not.toBeInTheDocument()
  })

  it('throws a helpful error outside the provider', () => {
    vi.spyOn(console, 'error').mockImplementation(() => {})
    expect(() => render(<Trigger />)).toThrow(/ToastProvider/)
  })
})

describe('VulnerabilityList', () => {
  it('lists findings highest risk first with fix versions', () => {
    render(<VulnerabilityList vulnerabilities={vulnerabilities} />)
    const rows = screen.getAllByRole('button', { expanded: false })
    expect(rows[0]).toHaveTextContent('CVE-2019-10744')
    expect(rows[0]).toHaveTextContent('4.17.12')
    expect(rows[2]).toHaveTextContent('No fix yet')
    expect(screen.getByText('Showing 3 of 3 findings, highest risk first')).toBeInTheDocument()
  })

  it('filters by severity chip, toggles and search', async () => {
    const user = userEvent.setup()
    render(<VulnerabilityList vulnerabilities={vulnerabilities} />)

    await user.click(screen.getByRole('button', { name: /High/, pressed: false }))
    expect(screen.getByText('Showing 1 of 3 findings, highest risk first')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /High/, pressed: true }))

    await user.click(screen.getByLabelText('Exploited'))
    expect(screen.getByText(/Showing 1 of 3/)).toBeInTheDocument()
    await user.click(screen.getByLabelText('Exploited'))

    await user.click(screen.getByLabelText('Hide dev'))
    expect(screen.getByText(/Showing 2 of 3/)).toBeInTheDocument()
    await user.click(screen.getByLabelText('Hide dev'))

    await user.type(screen.getByLabelText('Search vulnerabilities'), 'nothing-matches')
    expect(screen.getByText('No findings match these filters')).toBeInTheDocument()
    await user.click(screen.getByText('Clear filters'))
    expect(screen.getByText(/Showing 3 of 3/)).toBeInTheDocument()
  })

  it('expands a finding to show details and links', async () => {
    render(<VulnerabilityList vulnerabilities={vulnerabilities} />)
    await userEvent.click(screen.getAllByRole('button', { expanded: false })[0])
    expect(screen.getByText('lodash@4.17.12')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Advisory/ })).toHaveAttribute('href', 'https://osv.dev/vulnerability/GHSA-jf85-cpcp-j695')
    expect(screen.getByRole('link', { name: /CISA KEV entry/ })).toBeInTheDocument()
  })

  it('celebrates a clean result', () => {
    render(<VulnerabilityList vulnerabilities={[]} />)
    expect(screen.getByText('No known vulnerabilities')).toBeInTheDocument()
  })
})

describe('DependencyTable', () => {
  it('shows declared vs matched versions and filters', async () => {
    const user = userEvent.setup()
    render(<DependencyTable dependencies={dependencies} />)
    expect(screen.getAllByRole('row')).toHaveLength(4)
    expect(screen.getByText('unpinned')).toBeInTheDocument()

    await user.selectOptions(screen.getByLabelText('Filter by ecosystem'), 'PyPI')
    expect(screen.getAllByRole('row')).toHaveLength(2)
    await user.selectOptions(screen.getByLabelText('Filter by ecosystem'), 'all')

    await user.click(screen.getByLabelText('Vulnerable only'))
    expect(screen.getAllByRole('row')).toHaveLength(2)
    expect(screen.getByText('Showing 1 of 3 dependencies')).toBeInTheDocument()
  })

  it('handles an empty list', () => {
    render(<DependencyTable dependencies={[]} />)
    expect(screen.getByText('No dependencies found')).toBeInTheDocument()
  })
})

describe('RemediationPlan', () => {
  it('lists upgrades and hides itself when nothing is fixable', () => {
    const { rerender } = render(<RemediationPlan vulnerabilities={vulnerabilities} />)
    expect(screen.getByText('Fix first')).toBeInTheDocument()
    expect(screen.getByText('4.17.19')).toBeInTheDocument()
    expect(screen.getByText('fixes 2')).toBeInTheDocument()
    rerender(<RemediationPlan vulnerabilities={[vulnerabilities[2]]} />)
    expect(screen.queryByText('Fix first')).not.toBeInTheDocument()
  })
})
