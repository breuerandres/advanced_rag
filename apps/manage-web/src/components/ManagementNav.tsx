import {
  ClipboardList,
  DollarSign,
  FileText,
  MessageSquareWarning,
  Settings,
  ShieldCheck,
  Users,
} from 'lucide-react'

export type ManagementSection =
  | 'documents'
  | 'users'
  | 'audit'
  | 'feedback'
  | 'budgets'
  | 'configuration'

interface ManagementNavProps {
  active: ManagementSection
  onNavigate: (section: ManagementSection) => void
}

const links = [
  { id: 'documents', label: 'Documentos', icon: FileText },
  { id: 'users', label: 'Usuarios y grupos', icon: Users },
  { id: 'audit', label: 'Auditoria', icon: ClipboardList },
  { id: 'feedback', label: 'Feedback', icon: MessageSquareWarning },
  { id: 'budgets', label: 'Presupuestos IA', icon: DollarSign },
  { id: 'configuration', label: 'Configuracion', icon: Settings },
] satisfies Array<{
  id: ManagementSection
  label: string
  icon: typeof ShieldCheck
}>

export function ManagementNav({ active, onNavigate }: ManagementNavProps) {
  return (
    <aside className="sidebar" aria-label="Navegacion principal">
      <div className="sidebar-brand">
        <span className="brand-mark">AR</span>
        <span>Advanced RAG</span>
      </div>
      <nav>
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
    </aside>
  )
}
