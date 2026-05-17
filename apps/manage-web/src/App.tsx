import { useState } from 'react'
import { DocumentsPage } from './features/documents/DocumentsPage'
import { UsersBudgetPage } from './features/users/UsersBudgetPage'
import './App.css'

export default function App() {
  const [view, setView] = useState<'users' | 'documents'>('users')

  return view === 'documents' ? (
    <DocumentsPage onOpenUsers={() => setView('users')} />
  ) : (
    <UsersBudgetPage onOpenDocuments={() => setView('documents')} />
  )
}
