import { useState } from 'react'
import { DocumentsPage } from './features/documents/DocumentsPage'
import { FeedbackReviewPage } from './features/reporting/FeedbackReviewPage'
import { UsersBudgetPage } from './features/users/UsersBudgetPage'
import './App.css'

export default function App() {
  const [view, setView] = useState<'users' | 'documents' | 'feedback'>('users')

  if (view === 'documents') {
    return <DocumentsPage onOpenUsers={() => setView('users')} />
  }

  if (view === 'feedback') {
    return (
      <FeedbackReviewPage
        onOpenDocuments={() => setView('documents')}
        onOpenUsers={() => setView('users')}
      />
    )
  }

  return (
    <UsersBudgetPage
      onOpenDocuments={() => setView('documents')}
      onOpenFeedback={() => setView('feedback')}
    />
  )
}
