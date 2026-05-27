import { useEffect, useState } from 'react'
import type { FormEvent, ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { AlertCircle, CheckCircle2, LogOut } from 'lucide-react'
import {
  AppShell,
  AuthCardHeader,
  AuthShell,
  Button,
  DarkModeToggle,
  Input,
  LanguageSelect,
  Sidebar,
} from '@helpcenter/shared-ui'
import {
  createFirstAdmin,
  getSession,
  getSetupStatus,
  login,
  logout,
  type SessionUser,
  type SetupStatus,
} from './api/auth'
import { ManagementNav, type ManagementSection } from './components/ManagementNav'
import { AccountPage } from './features/account/AccountPage'
import { AuditPage } from './features/audit/AuditPage'
import { ConfigurationPage } from './features/configuration/ConfigurationPage'
import { DocumentsPage } from './features/documents/DocumentsPage'
import { FeedbackReviewPage } from './features/reporting/FeedbackReviewPage'
import { UsersBudgetPage } from './features/users/UsersBudgetPage'
import { ApiError } from './lib/api-error'
import './i18n'
import './App.css'

type AppMode = 'loading' | 'setup' | 'login' | 'authenticated' | 'unavailable'

export default function App() {
  const { t, i18n } = useTranslation()
  const [view, setView] = useState<ManagementSection>('users')
  const [mode, setMode] = useState<AppMode>('loading')
  const [setupStatus, setSetupStatus] = useState<SetupStatus | null>(null)
  const [sessionUser, setSessionUser] = useState<SessionUser | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [bootError, setBootError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false

    async function boot() {
      try {
        const status = await getSetupStatus()
        if (cancelled) {
          return
        }

        setSetupStatus(status)
        if (status.setupRequired) {
          setMode('setup')
          return
        }

        try {
          const session = await getSession()
          if (!cancelled) {
            setSessionUser(session.user)
            setMode('authenticated')
          }
        } catch (error) {
          if (cancelled) {
            return
          }

          if (error instanceof ApiError && error.code === 'AUTH_REQUIRED') {
            setMode('login')
            return
          }

          setBootError(formatApiError(error, 'No se pudo verificar la sesión.'))
          setMode('unavailable')
        }
      } catch (error) {
        if (!cancelled) {
          setBootError(formatApiError(error, 'No se pudo cargar el estado inicial.'))
          setMode('unavailable')
        }
      }
    }

    void boot()

    return () => {
      cancelled = true
    }
  }, [])

  if (mode === 'loading') {
    return <SplashState title="Cargando consola" detail="Verificando sesión y estado de instalación." />
  }

  if (mode === 'unavailable') {
    return (
      <AuthFrame>
        <StatusPanel
          tone="error"
          title="La consola no está disponible"
          detail={bootError ?? 'Revisá que la API y la base de datos estén listas.'}
        />
      </AuthFrame>
    )
  }

  if (mode === 'setup') {
    return (
      <SetupPage
        status={setupStatus}
        onCreated={() => {
          setNotice('Administrador creado. Iniciá sesión para continuar.')
          setMode('login')
        }}
      />
    )
  }

  if (mode === 'login' || sessionUser === null) {
    return (
      <LoginPage
        notice={notice}
        onAuthenticated={(user) => {
          setNotice(null)
          setSessionUser(user)
          setMode('authenticated')
        }}
      />
    )
  }

  async function handleLogout() {
    await logout()
    setSessionUser(null)
    setMode('login')
  }

  return (
    <AppShell
      sidebar={
        <section className="management-sidebar" aria-label="Navegacion principal">
          <Sidebar
            top={
              <div className="sidebar-brand">
                <span className="brand-mark">AR</span>
                <span>Advanced RAG</span>
              </div>
            }
            bottom={
              <section className="sidebar-session" aria-label={t('auth.active_session')}>
                <div className="sidebar-controls">
                  <LanguageSelect
                    label={t('common.language')}
                    value={i18n.resolvedLanguage ?? i18n.language}
                    onChange={(value) => void i18n.changeLanguage(value)}
                    options={[
                      { value: 'es-AR', label: 'ES' },
                      { value: 'en-US', label: 'EN' },
                    ]}
                  />
                  <DarkModeToggle label={t('common.toggle_theme')} />
                </div>
                <span className="session-label">{t('auth.active_session')}</span>
                <strong>{sessionUser.email}</strong>
                <span>{sessionUser.roles.join(', ')}</span>
                <Button className="sidebar-logout" type="button" onClick={() => void handleLogout()}>
                  <LogOut size={16} aria-hidden="true" />
                  {t('auth.logout')}
                </Button>
              </section>
            }
          >
            <ManagementNav active={view} onNavigate={setView} />
          </Sidebar>
        </section>
      }
      className="app-workspace"
    >
      <section className="app-workspace">
        {view === 'documents' ? <DocumentsPage userRoles={sessionUser.roles} /> : null}
        {view === 'users' ? <UsersBudgetPage /> : null}
        {view === 'audit' ? <AuditPage /> : null}
        {view === 'feedback' ? <FeedbackReviewPage /> : null}
        {view === 'configuration' ? <ConfigurationPage /> : null}
        {view === 'account' ? (
          <AccountPage user={sessionUser} onUserUpdated={setSessionUser} />
        ) : null}
      </section>
    </AppShell>
  )
}

function SetupPage({
  status,
  onCreated,
}: {
  status: SetupStatus | null
  onCreated: () => void
}) {
  const [email, setEmail] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [password, setPassword] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    setIsSubmitting(true)

    try {
      await createFirstAdmin({ email, displayName, password })
      onCreated()
    } catch (nextError) {
      setError(formatApiError(nextError, 'No se pudo crear el administrador inicial.'))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <AuthFrame>
      <form className="auth-card" onSubmit={submit}>
        <AuthCardHeader
          eyebrow="Primer uso"
          title="Configurá el primer administrador"
          detail="Creá una cuenta Admin para abrir la consola de gestión. Después de este paso, el registro público queda bloqueado."
        />
        <div className="setup-readiness" aria-label="Estado de instalación">
          <CheckCircle2 size={16} aria-hidden="true" />
          <span>{status?.databaseReady ? 'Base de datos lista' : 'Verificando base de datos'}</span>
          <span>Roles: {(status?.requiredRoles ?? ['Admin', 'DocumentManager', 'Viewer']).join(', ')}</span>
        </div>
        {error ? <p className="status-message error">{error}</p> : null}
        <label className="field">
          <span>Email</span>
          <Input
            autoComplete="email"
            type="email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
          />
        </label>
        <label className="field">
          <span>Nombre visible</span>
          <Input
            autoComplete="name"
            type="text"
            value={displayName}
            onChange={(event) => setDisplayName(event.target.value)}
          />
        </label>
        <label className="field">
          <span>Contraseña</span>
          <Input
            autoComplete="new-password"
            type="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
          />
        </label>
        <Button className="ui-button primary-button auth-submit" disabled={isSubmitting} type="submit">
          Crear administrador
        </Button>
      </form>
    </AuthFrame>
  )
}

function LoginPage({
  notice,
  onAuthenticated,
}: {
  notice: string | null
  onAuthenticated: (user: SessionUser) => void
}) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    setIsSubmitting(true)

    try {
      const session = await login({ email, password })
      onAuthenticated(session.user)
    } catch (nextError) {
      if (nextError instanceof ApiError && nextError.code === 'AUTH_REQUIRED') {
        setError('Email o contraseña no válidos.')
      } else {
        setError(formatApiError(nextError, 'No se pudo iniciar sesión.'))
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <AuthFrame>
      <form className="auth-card" onSubmit={submit}>
        <AuthCardHeader
          eyebrow="Consola de gestión"
          title="Ingresá a la consola"
          detail="Usá tu cuenta Admin o DocumentManager para administrar instrucciones, usuarios, auditoria y configuración."
        />
        {notice ? <p className="status-message success">{notice}</p> : null}
        {error ? <p className="status-message error">{error}</p> : null}
        <label className="field">
          <span>Email</span>
          <Input
            autoComplete="email"
            type="email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
          />
        </label>
        <label className="field">
          <span>Contraseña</span>
          <Input
            autoComplete="current-password"
            type="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
          />
        </label>
        <Button className="ui-button primary-button auth-submit" disabled={isSubmitting} type="submit">
          Ingresar
        </Button>
      </form>
    </AuthFrame>
  )
}

function AuthFrame({ children }: { children: ReactNode }) {
  return <AuthShell>{children}</AuthShell>
}

function StatusPanel({
  tone,
  title,
  detail,
}: {
  tone: 'error' | 'neutral'
  title: string
  detail: string
}) {
  return (
    <section className={`auth-card status-panel ${tone}`}>
      <AlertCircle size={20} aria-hidden="true" />
      <div>
        <h1>{title}</h1>
        <p>{detail}</p>
        <p className="muted-copy">
          En local, verificá Docker, Caddy y el certificado de `*.localhost`.
        </p>
      </div>
    </section>
  )
}

function SplashState({ title, detail }: { title: string; detail: string }) {
  return (
    <AuthFrame>
      <section className="auth-card status-panel neutral">
        <div className="loading-dot" aria-hidden="true" />
        <div>
          <h1>{title}</h1>
          <p>{detail}</p>
        </div>
      </section>
    </AuthFrame>
  )
}

function formatApiError(error: unknown, fallback: string): string {
  if (error instanceof ApiError) {
    return `${fallback} Referencia: ${error.requestId}.`
  }

  return fallback
}
