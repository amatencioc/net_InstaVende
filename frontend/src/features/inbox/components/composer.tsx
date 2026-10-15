import { Bot, Hand, RotateCcw, SendHorizontal } from 'lucide-react'
import { useRef, useState, type FormEvent, type KeyboardEvent } from 'react'
import type { ConversationStatus } from '../api'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

const MAX_LENGTH = 4096
/** Borradores por conversación (se conservan al cambiar de chat durante la sesión). */
const drafts = new Map<number, string>()

interface ComposerProps {
  conversationId: number
  status: ConversationStatus
  autoFocus: boolean
  onSend: (text: string) => void
  onReopen: () => void
  reopening: boolean
}

/** Caja de respuesta: Enter envía, Shift+Enter inserta un salto de línea. */
export function Composer({ conversationId, status, autoFocus, onSend, onReopen, reopening }: ComposerProps) {
  const [text, setText] = useState(() => drafts.get(conversationId) ?? '')
  const textareaRef = useRef<HTMLTextAreaElement>(null)
  const trimmed = text.trim()

  const update = (value: string) => {
    setText(value)
    if (value) drafts.set(conversationId, value)
    else drafts.delete(conversationId)
  }

  const submit = (e?: FormEvent) => {
    e?.preventDefault()
    if (!trimmed) return
    onSend(trimmed)
    update('')
    textareaRef.current?.focus()
  }

  const onKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey && !e.nativeEvent.isComposing) {
      e.preventDefault()
      submit()
    }
  }

  if (status === 'Resolved') {
    return (
      <div className="bg-card flex flex-col items-center justify-between gap-3 border-t px-4 py-3 sm:flex-row">
        <p className="text-muted-foreground text-sm">
          Esta conversación está resuelta. Reábrela para responder.
        </p>
        <Button size="sm" variant="outline" onClick={onReopen} disabled={reopening}>
          <RotateCcw /> Reabrir conversación
        </Button>
      </div>
    )
  }

  const hint =
    status === 'BotActive'
      ? {
          icon: Bot,
          text: 'El vendedor IA está atendiendo. Si respondes, tomarás el control de la conversación.',
        }
      : status === 'WaitingHuman'
        ? { icon: Hand, text: 'El cliente espera a una persona. Al responder tomarás el control.' }
        : null

  return (
    <form onSubmit={submit} className="bg-card border-t px-3 pt-2 pb-3 sm:px-4">
      {hint && (
        <p className="text-muted-foreground mb-2 flex items-center gap-1.5 text-xs">
          <hint.icon className="size-3.5 shrink-0" aria-hidden />
          {hint.text}
        </p>
      )}
      <div className="border-input focus-within:border-ring focus-within:ring-ring/50 dark:bg-input/30 flex items-end gap-2 rounded-xl border bg-transparent p-1.5 pl-3 shadow-xs transition-[color,box-shadow] focus-within:ring-[3px]">
        <label htmlFor={`composer-${conversationId}`} className="sr-only">
          Escribe un mensaje
        </label>
        <textarea
          id={`composer-${conversationId}`}
          ref={textareaRef}
          value={text}
          onChange={(e) => update(e.target.value)}
          onKeyDown={onKeyDown}
          placeholder="Escribe un mensaje…"
          rows={1}
          maxLength={MAX_LENGTH}
          autoFocus={autoFocus}
          aria-describedby={`composer-help-${conversationId}`}
          className="placeholder:text-muted-foreground field-sizing-content max-h-40 min-h-9 flex-1 resize-none bg-transparent py-2 text-base outline-none md:text-sm"
        />
        <Button
          type="submit"
          size="icon"
          className="shrink-0 rounded-lg"
          disabled={!trimmed}
          aria-label="Enviar mensaje"
        >
          <SendHorizontal />
        </Button>
      </div>
      <div
        id={`composer-help-${conversationId}`}
        className="text-muted-foreground mt-1.5 flex justify-between gap-2 px-1 text-[11px]"
      >
        <span className="hidden sm:inline">
          <kbd className="font-sans font-medium">Enter</kbd> para enviar ·{' '}
          <kbd className="font-sans font-medium">Shift + Enter</kbd> para nueva línea
        </span>
        {text.length > MAX_LENGTH - 500 && (
          <span className={cn('ml-auto tabular-nums', text.length >= MAX_LENGTH && 'text-destructive')}>
            {text.length}/{MAX_LENGTH}
          </span>
        )}
      </div>
    </form>
  )
}
