export interface RepoReference {
  owner: string
  name: string
}

const OWNER = /^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})$/
const REPO = /^[A-Za-z0-9._-]{1,100}$/

/**
 * Client-side mirror of the API's GitHubRepositoryUrl parser so users get instant feedback.
 * The server remains the source of truth.
 */
export function parseRepositoryInput(input: string): RepoReference | null {
  let value = input.trim()
  if (!value || value.length > 500) return null

  const ssh = /^git@github\.com:([^/\s]+)\/([^/\s]+?)\/?$/i.exec(value)
  let segments: string[]
  if (ssh) {
    segments = [ssh[1], ssh[2]]
  } else {
    value = value.split(/[?#]/)[0]
    const host = /^(?:https?:\/\/)?(?:www\.)?github\.com\//i.exec(value)
    if (host) value = value.slice(host[0].length)
    else if (value.includes('://') || value.split('/')[0].includes('.')) return null
    segments = value.split('/').filter(Boolean)
  }

  if (segments.length < 2) return null
  const owner = segments[0]
  const name = segments[1].replace(/\.git$/i, '')
  if (!OWNER.test(owner) || !REPO.test(name) || name === '.' || name === '..') return null
  return { owner, name }
}

export const githubUrl = (owner: string, name: string) => `https://github.com/${owner}/${name}`

export const EXAMPLE_REPOSITORIES = ['expressjs/express', 'pallets/flask', 'spring-projects/spring-petclinic', 'rails/rails']
