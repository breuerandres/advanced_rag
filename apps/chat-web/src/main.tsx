import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import '@helpcenter/shared-ui/styles/fonts.css'
import '@helpcenter/shared-ui/styles/tokens.css'
import '@helpcenter/shared-ui/styles/globals.css'
import './i18n'
import './index.css'
import App from './App.tsx'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
