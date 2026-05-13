import { FileText, LayoutGrid, Users } from 'lucide-react'
import './App.css'

export default function App() {
  return (
    <main className="app-shell">
      <header className="app-header">
        <div>
          <p className="eyebrow">Advanced RAG</p>
          <h1>Instruction Management</h1>
        </div>
        <div className="header-actions">
          <button type="button">Users</button>
          <button type="button">Documents</button>
        </div>
      </header>

      <section className="app-summary" aria-label="Instruction Management">
        <article>
          <LayoutGrid size={20} />
          <span>Operational workspace</span>
        </article>
        <article>
          <Users size={20} />
          <span>Users and groups</span>
        </article>
        <article>
          <FileText size={20} />
          <span>Lifecycle and audit</span>
        </article>
      </section>
    </main>
  )
}
