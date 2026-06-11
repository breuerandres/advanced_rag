import { DocumentPortalApp } from './features/portal/DocumentPortalApp'
import { ViewerLinkApp } from './features/viewer/ViewerLinkApp'
import './i18n'
import './App.css'

export default function App() {
  const params = new URLSearchParams(window.location.search)
  const documentId = params.get('documentId')
  const handoffCode = params.get('handoff')
  return documentId ? (
    <ViewerLinkApp documentId={documentId} handoffCode={handoffCode} />
  ) : (
    <DocumentPortalApp handoffCode={handoffCode} />
  )
}
