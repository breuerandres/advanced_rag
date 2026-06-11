import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import {
  AuthCardHeader,
  AuthShell,
  Button,
  DarkModeToggle,
  Input,
  LanguageSelect,
} from '@helpcenter/shared-ui'
import { login, type SessionUser } from '../../api/viewer'

export function DocsLoginPage({ onAuthenticated }: { onAuthenticated: (user: SessionUser) => void }) {
  const { t, i18n } = useTranslation()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [errorKey, setErrorKey] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setErrorKey(null)
    setIsSubmitting(true)
    try {
      const session = await login(email, password)
      onAuthenticated(session.user)
    } catch {
      setErrorKey('login.failed')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <AuthShell>
      <div className="auth-toolbar">
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
      <form className="auth-card" onSubmit={submit}>
        <AuthCardHeader eyebrow={t('login.eyebrow')} title={t('login.title')} />
        {errorKey ? <p className="status-message error">{t(errorKey)}</p> : null}
        <label className="field">
          <span>{t('login.email')}</span>
          <Input type="email" value={email} onChange={(event) => setEmail(event.target.value)} />
        </label>
        <label className="field">
          <span>{t('login.password')}</span>
          <Input
            type="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
          />
        </label>
        <Button className="primary-button" type="submit" disabled={isSubmitting}>
          {t('login.submit')}
        </Button>
      </form>
    </AuthShell>
  )
}
