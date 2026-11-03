import { describe, expect, it } from 'vitest'
import { parseRepositoryInput } from './github'
import { formatCompact, pluralize, relativeTime } from './format'
import { compareVersions, defaultDependencyFilter, defaultVulnerabilityFilter, filterDependencies, filterVulnerabilities, remediationPlan } from './filters'
import { vulnerabilitiesToCsv } from './csv'
import { isInProgress, severityRank } from './severity'
import { cn } from './cn'
import { dependencies, vulnerabilities, vulnerability } from '../test/fixtures'

describe('parseRepositoryInput', () => {
  it.each([
    ['expressjs/express', 'expressjs', 'express'],
    ['https://github.com/vercel/next.js', 'vercel', 'next.js'],
    ['github.com/pallets/flask.git', 'pallets', 'flask'],
    ['https://www.github.com/a/b/tree/main/src', 'a', 'b'],
    ['git@github.com:dotnet/runtime.git', 'dotnet', 'runtime'],
    ['https://github.com/a/b?tab=readme#top', 'a', 'b'],
  ])('accepts %s', (input, owner, name) => {
    expect(parseRepositoryInput(input)).toEqual({ owner, name })
  })

  it.each(['', '   ', 'express', 'https://gitlab.com/a/b', 'gitlab.com/a/b', '-bad/repo', 'owner/..', 'a/b c', 'x'.repeat(501)])('rejects %j', (input) => {
    expect(parseRepositoryInput(input)).toBeNull()
  })
})

describe('format', () => {
  const now = new Date('2026-06-01T12:00:00Z')
  it.each([
    ['2026-06-01T11:59:50Z', 'just now'],
    ['2026-06-01T11:55:00Z', '5 minutes ago'],
    ['2026-06-01T09:00:00Z', '3 hours ago'],
    ['2026-05-31T12:00:00Z', 'yesterday'],
    ['2026-05-18T12:00:00Z', '2 weeks ago'],
    ['2025-06-01T12:00:00Z', 'last year'],
  ])('relativeTime(%s) = %s', (value, expected) => {
    expect(relativeTime(value, now)).toBe(expected)
  })

  it('formats compact numbers and plurals', () => {
    expect(formatCompact(999)).toBe('999')
    expect(formatCompact(74_972)).toBe('75K')
    expect(pluralize(1, 'repository', 'repositories')).toBe('1 repository')
    expect(pluralize(1200, 'dependency', 'dependencies')).toBe('1,200 dependencies')
  })

  it('joins class names skipping falsy values', () => {
    expect(cn('a', false, null, undefined, 'b')).toBe('a b')
  })
})

describe('severity helpers', () => {
  it('ranks severities', () => {
    expect(severityRank('Critical')).toBeGreaterThan(severityRank('High'))
    expect(severityRank('Low')).toBeGreaterThan(severityRank(null))
  })

  it('detects in-progress statuses', () => {
    expect(['Pending', 'Parsing', 'FetchingVulnerabilities'].every((s) => isInProgress(s as never))).toBe(true)
    expect(isInProgress('Completed')).toBe(false)
    expect(isInProgress('Failed')).toBe(false)
  })
})

describe('filterVulnerabilities', () => {
  const ids = (filter: Partial<typeof defaultVulnerabilityFilter>) =>
    filterVulnerabilities(vulnerabilities, { ...defaultVulnerabilityFilter, ...filter }).map((v) => v.id)

  it('returns everything sorted by risk by default', () => expect(ids({})).toEqual(['v1', 'v2', 'v3']))
  it('filters by severity', () => expect(ids({ severities: ['High', 'Low'] })).toEqual(['v2', 'v3']))
  it('filters to fixable', () => expect(ids({ fixableOnly: true })).toEqual(['v1', 'v2']))
  it('filters to exploited', () => expect(ids({ exploitedOnly: true })).toEqual(['v1']))
  it('hides dev dependencies', () => expect(ids({ includeDev: false })).toEqual(['v1', 'v2']))
  it('searches id, package, title and aliases case-insensitively', () => {
    expect(ids({ query: 'zipobject' })).toEqual(['v2'])
    expect(ids({ query: 'JEST' })).toEqual(['v3'])
    expect(ids({ query: 'snyk-js' })).toEqual(['v1', 'v2'])
  })
  it('does not mutate the input', () => {
    const input = [...vulnerabilities].reverse()
    filterVulnerabilities(input, defaultVulnerabilityFilter)
    expect(input[0].id).toBe('v3')
  })
})

describe('filterDependencies', () => {
  const names = (filter: Partial<typeof defaultDependencyFilter>) =>
    filterDependencies(dependencies, { ...defaultDependencyFilter, ...filter }).map((d) => d.packageName)

  it('filters by ecosystem', () => expect(names({ ecosystem: 'PyPI' })).toEqual(['flask']))
  it('filters to vulnerable', () => expect(names({ vulnerableOnly: true })).toEqual(['lodash']))
  it('searches manifest paths', () => expect(names({ query: 'api/' })).toEqual(['flask']))
})

describe('remediationPlan', () => {
  it('collapses findings into one upgrade per package targeting the highest fix', () => {
    const plan = remediationPlan(vulnerabilities)
    expect(plan).toHaveLength(1)
    expect(plan[0]).toMatchObject({ packageName: 'lodash', from: '4.17.4', to: '4.17.19', fixes: 2, worst: 'Critical' })
  })

  it('orders by worst severity, then number of fixes', () => {
    const plan = remediationPlan([
      vulnerability({ id: 'a', packageName: 'low-pkg', severity: 'Low', remediationVersion: '2.0.0' }),
      vulnerability({ id: 'b', packageName: 'crit-pkg', severity: 'Critical', remediationVersion: '1.0.1' }),
      vulnerability({ id: 'c', packageName: 'low-pkg', severity: 'Medium', remediationVersion: '1.9.0' }),
    ])
    expect(plan.map((p) => p.packageName)).toEqual(['crit-pkg', 'low-pkg'])
    expect(plan[1]).toMatchObject({ worst: 'Medium', to: '2.0.0', fixes: 2 })
  })

  it('compares versions numerically', () => {
    expect(compareVersions('4.17.19', '4.17.4')).toBeGreaterThan(0)
    expect(compareVersions('1.10.0', '1.9.0')).toBeGreaterThan(0)
  })
})

describe('vulnerabilitiesToCsv', () => {
  it('writes a header and one row per finding', () => {
    const lines = vulnerabilitiesToCsv(vulnerabilities).split('\n')
    expect(lines[0]).toMatch(/^ID,Severity,CVSS/)
    expect(lines).toHaveLength(4)
    expect(lines[1]).toContain('CVE-2019-10744,Critical,9.1,95.5,lodash,4.17.4,NPM,4.17.12,yes')
  })

  it('quotes commas and neutralises spreadsheet formulas', () => {
    const csv = vulnerabilitiesToCsv([vulnerability({ title: '=HYPERLINK("http://evil")', packageName: 'a,b' })])
    expect(csv).toContain('"a,b"')
    expect(csv).toContain(`"'=HYPERLINK(""http://evil"")"`)
  })
})
