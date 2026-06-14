import {
  BookOpen,
  ClipboardList,
  FileText,
  MessageSquare,
  MessageSquareWarning,
  Network,
  Settings,
  ShieldCheck,
  Tags,
  UserRound,
  Users,
} from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { canAccessSection, buildProductSurfaceUrl } from './managementNavUtils'

export type ManagementSection =
  | 'documents'
  | 'document-types'
  | 'organizational-units'
  | 'users'
  | 'audit'
  | 'feedback'
  | 'configuration'
  | 'account'

export type ProductSurface = 'chat' | 'docs'

interface ManagementNavProps {
  active: ManagementSection
  onNavigate: (section: ManagementSection) => void
  onOpenProductSurface: (surface: ProductSurface) => void
  userRoles: string[]
}

const links = [
  { id: 'documents', labelKey: 'nav.documents', icon: FileText },
  { id: 'document-types', labelKey: 'nav.document_types', icon: Tags },
  { id: 'organizational-units', labelKey: 'nav.organizational_units', icon: Network },
  { id: 'users', labelKey: 'nav.users', icon: Users },
  { id: 'audit', labelKey: 'nav.audit', icon: ClipboardList },
  { id: 'feedback', labelKey: 'nav.feedback', icon: MessageSquareWarning },
  { id: 'configuration', labelKey: 'nav.configuration', icon: Settings },
  { id: 'account', labelKey: 'nav.account', icon: UserRound },
] satisfies Array<{
  id: ManagementSection
  labelKey: string
  icon: typeof ShieldCheck
}>

const productLinks = [
  { id: 'chat', labelKey: 'nav.chat', icon: MessageSquare },
  { id: 'docs', labelKey: 'nav.docs', icon: BookOpen },
] satisfies Array<{
  id: ProductSurface
  labelKey: string
  icon: typeof ShieldCheck
}>

export function ManagementNav({
  active,
  onNavigate,
  onOpenProductSurface,
  userRoles,
}: ManagementNavProps) {
  const { t } = useTranslation()
  const visibleLinks = links.filter((link) => canAccessSection(link.id, userRoles))

  return (
    <nav className="sidebar-nav">
      {visibleLinks.map((link) => {
        const Icon = link.icon
        return (
          <a
            key={link.id}
            className={link.id === active ? 'nav-link nav-link-active' : 'nav-link'}
            href={`#${link.id}`}
            onClick={(event) => {
              event.preventDefault()
              onNavigate(link.id)
            }}
          >
            <Icon size={18} />
            <span>{t(link.labelKey)}</span>
          </a>
        )
      })}
      <div className="sidebar-nav-separator" aria-hidden="true" />
      {productLinks.map((link) => {
        const Icon = link.icon
        return (
          <a
            key={link.id}
            className="nav-link"
            href={buildProductSurfaceUrl(link.id)}
            target="_blank"
            rel="noopener noreferrer"
            onClick={(event) => {
              event.preventDefault()
              onOpenProductSurface(link.id)
            }}
          >
            <Icon size={18} />
            <span>{t(link.labelKey)}</span>
          </a>
        )
      })}
    </nav>
  )
}
