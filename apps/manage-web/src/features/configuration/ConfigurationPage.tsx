import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { RefreshCw } from 'lucide-react'
import { Button } from '@helpcenter/shared-ui'
import {
  getOperationalConfiguration,
  updateOperationalConfiguration,
} from '../../api/configuration'
import type {
  OperationalConfiguration,
  UpdateOperationalConfigurationRequest,
} from '../../api/configuration'
import { ApiError } from '../../lib/api-error'

export function ConfigurationPage({ userRoles }: { userRoles: string[] }) {
  const { t } = useTranslation()
  const canEdit = userRoles.includes('Admin')
  const [configuration, setConfiguration] = useState<OperationalConfiguration | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [isEditing, setIsEditing] = useState(false)
  const [isSaving, setIsSaving] = useState(false)
  const [form, setForm] = useState<UpdateOperationalConfigurationRequest | null>(null)
  const [saveMessage, setSaveMessage] = useState<string | null>(null)
  const [saveError, setSaveError] = useState<string | null>(null)

  useEffect(() => {
    void loadConfiguration()
    // eslint-disable-next-line react-hooks/exhaustive-deps -- one-time mount load; loadConfiguration is stable within the component lifetime
  }, [])

  async function loadConfiguration() {
    setIsLoading(true)
    setErrorMessage(null)
    try {
      setConfiguration(await getOperationalConfiguration())
      setIsEditing(false)
    } catch (error) {
      const reference = error instanceof ApiError ? error.requestId : 'unknown'
      setErrorMessage(t('configuration.load_error', { reference }))
    } finally {
      setIsLoading(false)
    }
  }

  function startEditing() {
    if (!configuration) {
      return
    }
    setForm({
      chatModel: configuration.chatModel,
      customerTimezone: configuration.customerTimezone,
      defaultMonthlyAiBudgetUsd: configuration.defaultMonthlyAiBudgetUsd,
      semanticCacheTtlHours: configuration.semanticCacheTtlHours,
      semanticCacheSimilarityThreshold: configuration.semanticCacheSimilarityThreshold,
      chatMaxQuestionChars: configuration.chatMaxQuestionChars,
      importMaxFileSizeMb: configuration.importMaxFileSizeMb,
    })
    setSaveMessage(null)
    setSaveError(null)
    setIsEditing(true)
  }

  function cancelEditing() {
    setIsEditing(false)
    setForm(null)
    setSaveError(null)
  }

  async function saveConfiguration() {
    if (!form) {
      return
    }
    setIsSaving(true)
    setSaveMessage(null)
    setSaveError(null)
    try {
      const updated = await updateOperationalConfiguration(form)
      setConfiguration(updated)
      setIsEditing(false)
      setForm(null)
      setSaveMessage(t('configuration.saved'))
    } catch (error) {
      const reference = error instanceof ApiError ? error.requestId : 'unknown'
      setSaveError(t('configuration.save_error', { reference }))
    } finally {
      setIsSaving(false)
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
          disabled={isLoading || isSaving}
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
      {saveMessage ? (
        <p className="status-message" role="status">
          {saveMessage}
        </p>
      ) : null}
      {saveError ? (
        <p className="status-message error" role="alert">
          {saveError}
        </p>
      ) : null}
      {!canEdit ? <p className="muted-copy">{t('configuration.read_only_hint')}</p> : null}

      {configuration && isEditing && canEdit && form ? (
        <form
          className="settings-grid config-edit"
          onSubmit={(event) => {
            event.preventDefault()
            void saveConfiguration()
          }}
        >
          <section className="settings-panel" aria-label={t('configuration.ai_models')}>
            <h2>{t('configuration.ai_models')}</h2>
            <label className="field">
              <span>{t('configuration.provider')}</span>
              <input type="text" value="OpenAI" disabled readOnly />
            </label>
            <label className="field">
              <span>{t('configuration.chat_model')}</span>
              <input
                type="text"
                value={form.chatModel}
                onChange={(event) => setForm({ ...form, chatModel: event.target.value })}
              />
            </label>
            <div className="field">
              <span>Embeddings</span>
              <strong>{configuration.embeddingModel}</strong>
            </div>
            <div className="field">
              <span>{t('configuration.dimension')}</span>
              <strong>
                {t('configuration.dimensions_value', { count: configuration.embeddingDimensions })}
              </strong>
            </div>
            <p className="muted-copy">{t('configuration.fixed_hint')}</p>
          </section>

          <section className="settings-panel" aria-label={t('configuration.cache_limits')}>
            <h2>{t('configuration.cache_limits')}</h2>
            <label className="field">
              <span>{t('configuration.timezone')}</span>
              <input
                type="text"
                value={form.customerTimezone}
                onChange={(event) => setForm({ ...form, customerTimezone: event.target.value })}
              />
            </label>
            <label className="field">
              <span>{t('configuration.base_budget')}</span>
              <input
                type="number"
                step="0.01"
                min="0"
                value={form.defaultMonthlyAiBudgetUsd}
                onChange={(event) =>
                  setForm({ ...form, defaultMonthlyAiBudgetUsd: Number(event.target.value) })
                }
              />
            </label>
            <label className="field">
              <span>{t('configuration.cache_ttl')}</span>
              <input
                type="number"
                min="1"
                value={form.semanticCacheTtlHours}
                onChange={(event) =>
                  setForm({ ...form, semanticCacheTtlHours: Number(event.target.value) })
                }
              />
            </label>
            <label className="field">
              <span>{t('configuration.similarity_threshold')}</span>
              <input
                type="number"
                step="0.01"
                min="0"
                max="1"
                value={form.semanticCacheSimilarityThreshold}
                onChange={(event) =>
                  setForm({
                    ...form,
                    semanticCacheSimilarityThreshold: Number(event.target.value),
                  })
                }
              />
            </label>
            <label className="field">
              <span>{t('configuration.chat_question')}</span>
              <input
                type="number"
                min="1"
                value={form.chatMaxQuestionChars}
                onChange={(event) =>
                  setForm({ ...form, chatMaxQuestionChars: Number(event.target.value) })
                }
              />
            </label>
            <label className="field">
              <span>{t('configuration.import_limit')}</span>
              <input
                type="number"
                min="1"
                value={form.importMaxFileSizeMb}
                onChange={(event) =>
                  setForm({ ...form, importMaxFileSizeMb: Number(event.target.value) })
                }
              />
            </label>
          </section>

          <section className="settings-panel" aria-label={t('configuration.secret_values')}>
            <h2>{t('configuration.secret_values')}</h2>
            <p className="muted-copy">{t('configuration.secret_copy')}</p>
            <dl>
              {configuration.secrets.map((secret) => (
                <div key={secret.name}>
                  <dt>{secret.name}</dt>
                  <dd>{secret.status}</dd>
                </div>
              ))}
            </dl>
          </section>

          <div className="form-actions">
            <Button type="submit" loading={isSaving} disabled={isSaving}>
              {t('configuration.save')}
            </Button>
            <Button type="button" variant="ghost" disabled={isSaving} onClick={cancelEditing}>
              {t('configuration.cancel')}
            </Button>
          </div>
        </form>
      ) : null}

      {configuration && !(isEditing && canEdit) ? (
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
                  <dt>{t('configuration.provider')}</dt>
                  <dd>{configuration.llmProvider}</dd>
                </div>
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
              <p className="muted-copy">{t('configuration.secret_copy')}</p>
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

          {canEdit ? (
            <div className="form-actions">
              <Button type="button" disabled={isLoading} onClick={startEditing}>
                {t('configuration.edit')}
              </Button>
            </div>
          ) : null}
        </>
      ) : null}
    </section>
  )
}

function formatCurrency(value: number) {
  return `USD ${value.toFixed(2)}`
}
