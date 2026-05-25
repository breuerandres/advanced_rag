import { useEffect, useState, type FormEvent } from 'react'
import { KeyRound, Mail } from 'lucide-react'
import type { SessionUser } from '../../api/auth'
import { changeCurrentUserPassword, updateCurrentUserEmail } from '../../api/account'
import { ApiError } from '../../lib/api-error'

interface AccountPageProps {
  user: SessionUser
  onUserUpdated: (user: SessionUser) => void
}

export function AccountPage({ user, onUserUpdated }: AccountPageProps) {
  const [email, setEmail] = useState(user.email)
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [emailStatus, setEmailStatus] = useState<string | null>(null)
  const [passwordStatus, setPasswordStatus] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isSavingEmail, setIsSavingEmail] = useState(false)
  const [isSavingPassword, setIsSavingPassword] = useState(false)

  useEffect(() => {
    setEmail(user.email)
  }, [user.email])

  async function submitEmail(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    setEmailStatus(null)
    setIsSavingEmail(true)

    try {
      const session = await updateCurrentUserEmail({ email })
      onUserUpdated(session.user)
      setEmailStatus('Email actualizado.')
    } catch (caught) {
      setError(formatApiError(caught, 'No se pudo actualizar el email.'))
    } finally {
      setIsSavingEmail(false)
    }
  }

  async function submitPassword(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    setPasswordStatus(null)
    setIsSavingPassword(true)

    try {
      await changeCurrentUserPassword({ currentPassword, newPassword })
      setCurrentPassword('')
      setNewPassword('')
      setPasswordStatus('Contraseña actualizada.')
    } catch (caught) {
      setError(formatApiError(caught, 'No se pudo cambiar la contraseña.'))
    } finally {
      setIsSavingPassword(false)
    }
  }

  return (
    <section className="workspace account-workspace" id="account">
      <header className="workspace-header">
        <div>
          <p className="eyebrow">Sesion</p>
          <h1>Mi cuenta</h1>
        </div>
      </header>

      {error ? (
        <p className="status-message error" role="alert">
          {error}
        </p>
      ) : null}

      <div className="settings-grid">
        <form className="settings-panel" onSubmit={submitEmail}>
          <header className="settings-panel-header">
            <Mail size={18} aria-hidden="true" />
            <h2>Email</h2>
          </header>
          <label className="field">
            <span>Email</span>
            <input
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              disabled={isSavingEmail}
            />
          </label>
          <button className="ui-button primary-button" type="submit" disabled={isSavingEmail}>
            Guardar email
          </button>
          {emailStatus ? (
            <p className="status-message success" role="status">
              {emailStatus}
            </p>
          ) : null}
        </form>

        <form className="settings-panel" onSubmit={submitPassword}>
          <header className="settings-panel-header">
            <KeyRound size={18} aria-hidden="true" />
            <h2>Contraseña</h2>
          </header>
          <label className="field">
            <span>Contraseña actual</span>
            <input
              type="password"
              autoComplete="current-password"
              value={currentPassword}
              onChange={(event) => setCurrentPassword(event.target.value)}
              disabled={isSavingPassword}
            />
          </label>
          <label className="field">
            <span>Nueva contraseña</span>
            <input
              type="password"
              autoComplete="new-password"
              value={newPassword}
              onChange={(event) => setNewPassword(event.target.value)}
              disabled={isSavingPassword}
            />
          </label>
          <button className="ui-button primary-button" type="submit" disabled={isSavingPassword}>
            Cambiar contraseña
          </button>
          {passwordStatus ? (
            <p className="status-message success" role="status">
              {passwordStatus}
            </p>
          ) : null}
        </form>
      </div>
    </section>
  )
}

function formatApiError(error: unknown, fallback: string): string {
  if (error instanceof ApiError) {
    return `${fallback} Referencia: ${error.requestId}.`
  }

  return fallback
}
