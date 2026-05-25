import {
  ClipboardList,
  FileText,
  MessageSquareWarning,
  Settings,
  ShieldCheck,
  UserRound,
  Users,
} from 'lucide-react'

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
  { id: 'documents', label: 'Documentos', icon: FileText },
  { id: 'users', label: 'Usuarios y grupos', icon: Users },
  { id: 'audit', label: 'Auditoria', icon: ClipboardList },
  { id: 'feedback', label: 'Feedback', icon: MessageSquareWarning },
  { id: 'configuration', label: 'Configuracion', icon: Settings },
  { id: 'account', label: 'Mi cuenta', icon: UserRound },
] satisfies Array<{
  id: ManagementSection
  label: string
  icon: typeof ShieldCheck
}>

export function ManagementNav({ active, onNavigate }: ManagementNavProps) {
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
            <span>{link.label}</span>
          </a>
        )
      })}
    </nav>
  )
}
