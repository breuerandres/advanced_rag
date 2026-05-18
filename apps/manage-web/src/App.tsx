import { useState } from 'react'
import { ManagementNav, type ManagementSection } from './components/ManagementNav'
import { AuditPage } from './features/audit/AuditPage'
import { ConfigurationPage } from './features/configuration/ConfigurationPage'
import { DocumentsPage } from './features/documents/DocumentsPage'
import { FeedbackReviewPage } from './features/reporting/FeedbackReviewPage'
import { UsersBudgetPage } from './features/users/UsersBudgetPage'
import './App.css'

export default function App() {
  const [view, setView] = useState<ManagementSection>('users')

  return (
    <main className="app-shell">
      <ManagementNav active={view} onNavigate={setView} />
      {view === 'documents' ? <DocumentsPage /> : null}
      {view === 'users' || view === 'budgets' ? <UsersBudgetPage /> : null}
      {view === 'audit' ? <AuditPage /> : null}
      {view === 'feedback' ? <FeedbackReviewPage /> : null}
      {view === 'configuration' ? <ConfigurationPage /> : null}
    </main>
  )
}
