import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { RefreshCw } from 'lucide-react'
import { Button } from '@helpcenter/shared-ui'
import { getOperationalConfiguration } from '../../api/configuration'
import type { OperationalConfiguration } from '../../api/configuration'
import { ApiError } from '../../lib/api-error'

export function ConfigurationPage() {
  const { t } = useTranslation()
  const [configuration, setConfiguration] = useState<OperationalConfiguration | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)

  useEffect(() => {
    void loadConfiguration()
    // eslint-disable-next-line react-hooks/exhaustive-deps -- one-time mount load; loadConfiguration is stable within the component lifetime
  }, [])

  async function loadConfiguration() {
    setIsLoading(true)
    setErrorMessage(null)
    try {
      setConfiguration(await getOperationalConfiguration())
    } catch (error) {
      const reference = error instanceof ApiError ? error.requestId : 'unknown'
      setErrorMessage(t('configuration.load_error', { reference }))
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <section className="workspace" id="configuration">
      <header className="workspace-header">
        <div>
          <p className="eyebrow">{t('configuration.eyebrow')}</p>
          <h1>{t('configuration.title')}</h1>
        </div>
        <Button
          className="icon-button"
          type="button"
          aria-label={t('configuration.refresh')}
          disabled={isLoading}
          onClick={() => void loadConfiguration()}
        >
          <RefreshCw size={18} />
        </Button>
      </header>

      {isLoading ? <p className="status-message">{t('configuration.loading')}</p> : null}
      {errorMessage ? (
        <p className="status-message error" role="alert">
          {errorMessage}
        </p>
      ) : null}

      {configuration ? (
        <>
          <section className="metrics-row config-summary" aria-label={t('configuration.summary_label')}>
            <div>
              <span className="metric-label">{t('configuration.timezone')}</span>
              <strong>{configuration.customerTimezone}</strong>
            </div>
            <div>
              <span className="metric-label">{t('configuration.base_budget')}</span>
              <strong>{formatCurrency(configuration.defaultMonthlyAiBudgetUsd)}</strong>
            </div>
            <div>
              <span className="metric-label">{t('configuration.import_limit')}</span>
              <strong>{configuration.importMaxFileSizeMb} MB</strong>
            </div>
          </section>

          <div className="settings-grid">
            <section className="settings-panel" aria-label={t('configuration.ai_models')}>
              <h2>{t('configuration.ai_models')}</h2>
              <dl>
                <div>
                  <dt>Chat</dt>
                  <dd>{configuration.chatModel}</dd>
                </div>
                <div>
                  <dt>Embeddings</dt>
                  <dd>{configuration.embeddingModel}</dd>
                </div>
                <div>
                  <dt>{t('configuration.dimension')}</dt>
                  <dd>{t('configuration.dimensions_value', { count: configuration.embeddingDimensions })}</dd>
                </div>
              </dl>
            </section>

            <section className="settings-panel" aria-label={t('configuration.cache_limits')}>
              <h2>{t('configuration.cache_limits')}</h2>
              <dl>
                <div>
                  <dt>{t('configuration.cache_ttl')}</dt>
                  <dd>{t('configuration.hours_value', { count: configuration.semanticCacheTtlHours })}</dd>
                </div>
                <div>
                  <dt>{t('configuration.similarity_threshold')}</dt>
                  <dd>{configuration.semanticCacheSimilarityThreshold.toFixed(2)}</dd>
                </div>
                <div>
                  <dt>{t('configuration.chat_question')}</dt>
                  <dd>{t('configuration.characters_value', { count: configuration.chatMaxQuestionChars })}</dd>
                </div>
              </dl>
            </section>

            <section className="settings-panel" aria-label={t('configuration.secret_values')}>
              <h2>{t('configuration.secret_values')}</h2>
              <p className="muted-copy">
                {t('configuration.secret_copy')}
              </p>
              <dl>
                {configuration.secrets.map((secret) => (
                  <div key={secret.name}>
                    <dt>{secret.name}</dt>
                    <dd>{secret.status}</dd>
                  </div>
                ))}
              </dl>
            </section>
          </div>
        </>
      ) : null}
    </section>
  )
}

function formatCurrency(value: number) {
  return `USD ${value.toFixed(2)}`
}
