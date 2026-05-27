import {
  ClipboardList,
  FileText,
  MessageSquareWarning,
  Settings,
  ShieldCheck,
  UserRound,
  Users,
} from 'lucide-react'
import { useTranslation } from 'react-i18next'

export type ManagementSection =
  | 'documents'
  | 'users'
  | 'audit'
  | 'feedback'
  | 'configuration'
  | 'account'

interface ManagementNavProps {
  active: ManagementSection
  onNavigate: (section: ManagementSection) => void
}

const links = [
  { id: 'documents', labelKey: 'nav.documents', icon: FileText },
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

export function ManagementNav({ active, onNavigate }: ManagementNavProps) {
  const { t } = useTranslation()

  return (
    <nav className="sidebar-nav">
      {links.map((link) => {
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
    </nav>
  )
}
