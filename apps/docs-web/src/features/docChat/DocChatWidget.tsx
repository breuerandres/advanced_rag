import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Eraser, RotateCcw, Sparkles, ThumbsDown, ThumbsUp, X } from 'lucide-react'
import { Button, ChatComposer, Input, Markdown } from '@helpcenter/shared-ui'
import { useDocChat, type DocChatTurn } from './useDocChat'

export function DocChatWidget({ documentId }: { documentId: string }) {
  const { t, i18n } = useTranslation()
  const locale = i18n.resolvedLanguage ?? i18n.language
  const [isOpen, setIsOpen] = useState(false)
  const chat = useDocChat(documentId, locale)
  const endRef = useRef<HTMLDivElement | null>(null)

  useEffect(() => {
    endRef.current?.scrollIntoView?.({ block: 'end' })
  }, [chat.turns, isOpen])

  return (
    <div className="doc-chat">
      {isOpen ? (
        <section className="doc-chat-panel" role="dialog" aria-label={t('docChat.title')}>
          <header className="doc-chat-header">
            <div>
              <h2>{t('docChat.title')}</h2>
              <p>{t('docChat.subtitle')}</p>
            </div>
            <div className="doc-chat-header-actions">
              {(chat.turns.length > 0 || !!chat.failure) ? (
                <button
                  type="button"
                  className="icon-button"
                  onClick={chat.clearConversation}
                  disabled={chat.isStreaming}
                  aria-label={t('docChat.clear')}
                  title={t('docChat.clear')}
                >
                  <Eraser size={15} aria-hidden="true" />
                </button>
              ) : null}
              <button
                type="button"
                className="icon-button"
                onClick={() => setIsOpen(false)}
                aria-label={t('docChat.close')}
              >
                <X size={16} aria-hidden="true" />
              </button>
            </div>
          </header>
          <div className="doc-chat-transcript" aria-live="polite">
            {chat.turns.length === 0 && !chat.failure ? (
              <div className="doc-chat-empty">
                <p>{t('docChat.empty_hint')}</p>
                <button
                  type="button"
                  className="suggestion"
                  onClick={() => void chat.ask(t('docChat.suggestion_1'))}
                >
                  {t('docChat.suggestion_1')}
                </button>
                <button
                  type="button"
                  className="suggestion"
                  onClick={() => void chat.ask(t('docChat.suggestion_2'))}
                >
                  {t('docChat.suggestion_2')}
                </button>
              </div>
            ) : null}
            {chat.turns.map((turn) => (
              <DocChatTurnView
                key={turn.key}
                turn={turn}
                onFeedbackValue={chat.setFeedbackValue}
                onFeedbackComment={chat.setFeedbackComment}
                onSendFeedback={chat.sendFeedback}
              />
            ))}
            {chat.isStreaming && chat.turns.at(-1)?.status === 'pending' ? (
              <p className="doc-chat-typing" aria-hidden="true">
                <span />
                <span />
                <span />
              </p>
            ) : null}
            {chat.failure ? (
              <div className="doc-chat-error" role="alert">
                <p>{t(chat.failure.messageKey)}</p>
                <Button type="button" className="ghost-button" onClick={chat.retry}>
                  <RotateCcw size={14} aria-hidden="true" />
                  {t('docChat.retry')}
                </Button>
              </div>
            ) : null}
            <div ref={endRef} />
          </div>
          <footer className="doc-chat-composer">
            <ChatComposer
              onSubmit={(question) => void chat.ask(question)}
              disabled={chat.isStreaming}
              placeholder={t('docChat.placeholder')}
              submitLabel={t('docChat.send')}
              pendingLabel={chat.isStreaming ? t('docChat.sending') : undefined}
            />
          </footer>
        </section>
      ) : null}
      <button
        type="button"
        className="doc-chat-fab"
        onClick={() => setIsOpen((open) => !open)}
        aria-expanded={isOpen}
        aria-label={isOpen ? t('docChat.close') : t('docChat.open')}
        title={isOpen ? t('docChat.close') : t('docChat.open')}
      >
        {isOpen ? <X size={22} aria-hidden="true" /> : <Sparkles size={22} aria-hidden="true" />}
      </button>
    </div>
  )
}

function DocChatTurnView({
  turn,
  onFeedbackValue,
  onFeedbackComment,
  onSendFeedback,
}: {
  turn: DocChatTurn
  onFeedbackValue: (key: string, value: 'up' | 'down') => void
  onFeedbackComment: (key: string, comment: string) => void
  onSendFeedback: (key: string) => void
}) {
  const { t } = useTranslation()
  return (
    <div className="doc-chat-turn">
      <p className="bubble user">{turn.question}</p>
      {turn.answer ? (
        <div className="bubble assistant">
          <Markdown content={turn.answer} />
          {turn.status === 'done' && turn.queryAuditEventId ? (
            <div className="doc-chat-feedback">
              <button
                type="button"
                className={turn.feedback.value === 'up' ? 'feedback-button selected' : 'feedback-button'}
                aria-pressed={turn.feedback.value === 'up'}
                aria-label={t('docChat.feedback_up')}
                title={t('docChat.feedback_up')}
                onClick={() => onFeedbackValue(turn.key, 'up')}
              >
                <ThumbsUp size={14} aria-hidden="true" />
              </button>
              <button
                type="button"
                className={turn.feedback.value === 'down' ? 'feedback-button selected' : 'feedback-button'}
                aria-pressed={turn.feedback.value === 'down'}
                aria-label={t('docChat.feedback_down')}
                title={t('docChat.feedback_down')}
                onClick={() => onFeedbackValue(turn.key, 'down')}
              >
                <ThumbsDown size={14} aria-hidden="true" />
              </button>
              {turn.feedback.status === 'sent' ? (
                <span className="feedback-note">{t('docChat.feedback_thanks')}</span>
              ) : null}
              {turn.feedback.status === 'error' ? (
                <span className="feedback-note error">{t('docChat.feedback_error')}</span>
              ) : null}
              {turn.feedback.value && turn.feedback.status !== 'sent' ? (
                <div className="feedback-comment">
                  <Input
                    value={turn.feedback.comment}
                    onChange={(event) => onFeedbackComment(turn.key, event.target.value)}
                    placeholder={t('docChat.feedback_comment_placeholder')}
                  />
                  <Button
                    type="button"
                    className="primary-button"
                    disabled={turn.feedback.status === 'sending'}
                    onClick={() => void onSendFeedback(turn.key)}
                  >
                    {t('docChat.feedback_send')}
                  </Button>
                </div>
              ) : null}
            </div>
          ) : null}
        </div>
      ) : null}
    </div>
  )
}
