import { useEffect, useState } from 'react'
import { RefreshCw } from 'lucide-react'
import { Button } from '@helpcenter/shared-ui'
import { getOperationalConfiguration } from '../../api/configuration'
import type { OperationalConfiguration } from '../../api/configuration'
import { ApiError } from '../../lib/api-error'

export function ConfigurationPage() {
  const [configuration, setConfiguration] = useState<OperationalConfiguration | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)

  useEffect(() => {
    void loadConfiguration()
  }, [])

  async function loadConfiguration() {
    setIsLoading(true)
    setErrorMessage(null)
    try {
      setConfiguration(await getOperationalConfiguration())
    } catch (error) {
      const reference = error instanceof ApiError ? error.requestId : 'unknown'
      setErrorMessage(`No se pudo cargar la configuracion. Referencia: ${reference}.`)
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <section className="workspace" id="configuration">
      <header className="workspace-header">
        <div>
          <p className="eyebrow">Operaciones</p>
          <h1>Configuracion operativa</h1>
        </div>
        <Button
          className="icon-button"
          type="button"
          aria-label="Actualizar configuracion"
          disabled={isLoading}
          onClick={() => void loadConfiguration()}
        >
          <RefreshCw size={18} />
        </Button>
      </header>

      {isLoading ? <p className="status-message">Cargando configuracion...</p> : null}
      {errorMessage ? (
        <p className="status-message error" role="alert">
          {errorMessage}
        </p>
      ) : null}

      {configuration ? (
        <>
          <section className="metrics-row config-summary" aria-label="Configuracion principal">
            <div>
              <span className="metric-label">Zona horaria</span>
              <strong>{configuration.customerTimezone}</strong>
            </div>
            <div>
              <span className="metric-label">Presupuesto mensual base</span>
              <strong>{formatCurrency(configuration.defaultMonthlyAiBudgetUsd)}</strong>
            </div>
            <div>
              <span className="metric-label">Limite de importacion</span>
              <strong>{configuration.importMaxFileSizeMb} MB</strong>
            </div>
          </section>

          <div className="settings-grid">
            <section className="settings-panel" aria-label="Modelos de IA">
              <h2>Modelos de IA</h2>
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
                  <dt>Dimension</dt>
                  <dd>{configuration.embeddingDimensions} dimensiones</dd>
                </div>
              </dl>
            </section>

            <section className="settings-panel" aria-label="Cache y limites">
              <h2>Cache y limites</h2>
              <dl>
                <div>
                  <dt>TTL de cache semantica</dt>
                  <dd>{configuration.semanticCacheTtlHours} horas</dd>
                </div>
                <div>
                  <dt>Umbral de similitud</dt>
                  <dd>{configuration.semanticCacheSimilarityThreshold.toFixed(2)}</dd>
                </div>
                <div>
                  <dt>Pregunta de chat</dt>
                  <dd>{configuration.chatMaxQuestionChars} caracteres</dd>
                </div>
              </dl>
            </section>

            <section className="settings-panel" aria-label="Valores secretos">
              <h2>Valores protegidos por secretos</h2>
              <p className="muted-copy">
                La consola muestra estado operativo, no valores sensibles.
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
