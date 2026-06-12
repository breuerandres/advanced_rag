import type { ManagementSection, ProductSurface } from './ManagementNav'

export function allowedManagementSections(userRoles: string[]): ManagementSection[] {
  const sections: ManagementSection[] = [
    'documents',
    'organizational-units',
    'users',
    'audit',
    'feedback',
    'configuration',
    'account',
  ]
  return sections.filter((section) => canAccessSection(section, userRoles))
}

export function canAccessSection(section: ManagementSection, userRoles: string[]): boolean {
  if (section === 'account' || section === 'configuration') {
    return true
  }

  if (section === 'organizational-units') {
    return userRoles.includes('Admin')
  }

  return (
    userRoles.includes('Admin') ||
    userRoles.includes('DocumentEditor') ||
    userRoles.includes('DocumentPublisher')
  )
}

export function buildProductSurfaceUrl(surface: ProductSurface): string {
  const origin =
    typeof window.location.origin === 'string' && window.location.origin.length > 0
      ? window.location.origin
      : 'https://manage.localhost'
  const url = new URL(origin)
  const labels = url.hostname.split('.')

  if (labels[0] === 'manage') {
    labels[0] = surface
    url.hostname = labels.join('.')
  } else if (url.hostname === 'localhost' || url.hostname === '127.0.0.1') {
    url.protocol = 'https:'
    url.hostname = `${surface}.localhost`
    url.port = ''
  } else {
    url.hostname = `${surface}.${url.hostname}`
  }

  url.pathname = '/'
  url.search = ''
  url.hash = ''

  return url.toString()
}
