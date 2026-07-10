import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import {
  AuthCardHeader,
  AuthFrame,
  AuthSurfaceControls,
  Button,
  Input,
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
    <AuthFrame
      ariaLabel={t('login.access_controls')}
      controls={
        <AuthSurfaceControls
          languageLabel={t('common.language')}
          language={i18n.resolvedLanguage ?? i18n.language}
          onLanguageChange={(value) => void i18n.changeLanguage(value)}
          themeLabel={t('common.toggle_theme')}
        />
      }
    >
      <form className="auth-card" onSubmit={submit}>
        <AuthCardHeader
          eyebrow={t('login.eyebrow')}
          title={t('login.title')}
          detail={t('login.detail')}
        />
        {errorKey ? <p className="status-message error">{t(errorKey)}</p> : null}
        <label className="auth-field">
          <span>{t('login.email')}</span>
          <Input
            autoComplete="email"
            type="email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
          />
        </label>
        <label className="auth-field">
          <span>{t('login.password')}</span>
          <Input
            autoComplete="current-password"
            type="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
          />
        </label>
        <Button className="auth-submit" type="submit" disabled={isSubmitting}>
          {t('login.submit')}
        </Button>
      </form>
    </AuthFrame>
  )
}
