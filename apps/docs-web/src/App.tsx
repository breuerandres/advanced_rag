import { BookOpen, Clock3, ShieldCheck } from 'lucide-react'
import './App.css'

export default function App() {
  return (
    <main className="app-shell">
      <header className="app-header">
        <div>
          <p className="eyebrow">Advanced RAG</p>
          <h1>Instruction Viewer</h1>
        </div>
        <div className="header-actions">
          <button type="button">Open</button>
          <button type="button">Access</button>
        </div>
      </header>

      <section className="app-summary" aria-label="Instruction Viewer">
        <article>
          <BookOpen size={20} />
          <span>Published content</span>
        </article>
        <article>
          <ShieldCheck size={20} />
          <span>Scoped access</span>
        </article>
        <article>
          <Clock3 size={20} />
          <span>Token-gated links</span>
        </article>
      </section>
    </main>
  )
}
