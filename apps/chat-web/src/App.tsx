import { MessageSquareText, Shield, Sparkles } from 'lucide-react'
import './App.css'

export default function App() {
  return (
    <main className="app-shell">
      <header className="app-header">
        <div>
          <p className="eyebrow">Advanced RAG</p>
          <h1>Instruction Chat</h1>
        </div>
        <div className="header-actions">
          <button type="button">Ask</button>
          <button type="button">Feedback</button>
        </div>
      </header>

      <section className="app-summary" aria-label="Instruction Chat">
        <article>
          <MessageSquareText size={20} />
          <span>Questions and answers</span>
        </article>
        <article>
          <Shield size={20} />
          <span>Scoped access</span>
        </article>
        <article>
          <Sparkles size={20} />
          <span>Citations and feedback</span>
        </article>
      </section>
    </main>
  )
}
