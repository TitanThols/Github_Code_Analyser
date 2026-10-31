import type { Vulnerability } from '../types'

const escape = (value: unknown) => {
  const text = value === null || value === undefined ? '' : String(value)
  // Neutralise spreadsheet formula injection (=, +, -, @) and quote when needed.
  const safe = /^[=+\-@\t\r]/.test(text) ? `'${text}` : text
  return /[",\n\r]/.test(safe) ? `"${safe.replaceAll('"', '""')}"` : safe
}

export function vulnerabilitiesToCsv(rows: Vulnerability[]): string {
  const header = ['ID', 'Severity', 'CVSS', 'Risk score', 'Package', 'Version', 'Ecosystem', 'Fixed in', 'Known exploited', 'Title', 'Reference']
  const lines = rows.map((v) =>
    [v.cveId, v.severity, v.cvssScore, v.riskScore, v.packageName, v.packageVersion, v.ecosystem, v.remediationVersion, v.isExploitable ? 'yes' : 'no', v.title, v.referenceUrl]
      .map(escape)
      .join(','),
  )
  return [header.join(','), ...lines].join('\n')
}

export function downloadText(fileName: string, content: string, type = 'text/csv') {
  const url = URL.createObjectURL(new Blob([content], { type }))
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  link.click()
  URL.revokeObjectURL(url)
}
