import {
  ClipboardList,
  FileText,
  LogOut,
  MessageSquareWarning,
  Settings,
  ShieldCheck,
  Users,
} from 'lucide-react'
import type { SessionUser } from '../api/auth'

export type ManagementSection =
  | 'documents'
  | 'users'
  | 'audit'
  | 'feedback'
  | 'configuration'

interface ManagementNavProps {
  active: ManagementSection
  sessionUser: SessionUser
  onLogout: () => void | Promise<void>
  onNavigate: (section: ManagementSection) => void
}

const links = [
  { id: 'documents', label: 'Documentos', icon: FileText },
  { id: 'users', label: 'Usuarios y grupos', icon: Users },
  { id: 'audit', label: 'Auditoria', icon: ClipboardList },
  { id: 'feedback', label: 'Feedback', icon: MessageSquareWarning },
  { id: 'configuration', label: 'Configuracion', icon: Settings },
] satisfies Array<{
  id: ManagementSection
  label: string
  icon: typeof ShieldCheck
}>

export function ManagementNav({ active, sessionUser, onLogout, onNavigate }: ManagementNavProps) {
  return (
    <aside className="sidebar" aria-label="Navegacion principal">
      <div>
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
      </div>

      <section className="sidebar-session" aria-label="Sesion activa">
        <span className="session-label">Sesión activa</span>
        <strong>{sessionUser.email}</strong>
        <span>{sessionUser.roles.join(', ')}</span>
        <button className="sidebar-logout" type="button" onClick={() => void onLogout()}>
          <LogOut size={16} aria-hidden="true" />
          Cerrar sesión
        </button>
      </section>
    </aside>
  )
}
