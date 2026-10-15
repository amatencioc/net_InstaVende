import { useQuery } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { ArrowRightLeft, Eraser, FlaskConical, Lightbulb, RotateCcw, SendHorizontal } from 'lucide-react'
import { useEffect, useRef, useState, type FormEvent, type KeyboardEvent } from 'react'
import { Alert } from '@/components/shared/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Textarea } from '@/components/ui/textarea'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { ApiError } from '@/lib/api/client'
import { cn } from '@/lib/utils'
import { sellerProfileQuery, sellerRulesQuery, useSellerTest } from '../api'
import { nowTime } from '../utils'
import { ChatBubble, ChatHeader, ChatNotice, ChatSurface, TypingIndicator } from './chat'

const SUGGESTIONS = [
  'Hola',
  '¿Qué productos tienes?',
  '#catalogo',
  '¿Hacen envíos a mi ciudad?',
  '¿Me haces un descuento?',
  'Quiero hablar con un asesor',
]

const MAX_LENGTH = 1000

type ChatItem =
  | { id: number; kind: 'user' | 'bot'; text: string; time: string }
  | { id: number; kind: 'notice'; text: string; tone: 'default' | 'warning' | 'info'; retry?: string }

type NewChatItem = ChatItem extends infer T ? (T extends unknown ? Omit<T, 'id'> : never) : never

/** Pestaña "Probar vendedor": chat de vista previa con la configuración guardada. */
export function TestChatPage() {
  const profile = useQuery(sellerProfileQuery)
  const rules = useQuery(sellerRulesQuery)
  const test = useSellerTest()
  const [items, setItems] = useState<ChatItem[]>([])
  const [draft, setDraft] = useState('')
  const nextId = useRef(0)
  const scrollRef = useRef<HTMLDivElement>(null)
  const inputRef = useRef<HTMLTextAreaElement>(null)

  const vendorName = profile.data?.profile.vendorName.trim() || rules.data?.botName || 'Tu vendedor'
  const isPaused = rules.data?.isActive === false

  // Mantiene visible el último mensaje.
  useEffect(() => {
    const el = scrollRef.current
    if (el) el.scrollTo({ top: el.scrollHeight, behavior: 'smooth' })
  }, [items.length, test.isPending])

  function push(...entries: NewChatItem[]) {
    setItems((prev) => [...prev, ...entries.map((e) => ({ ...e, id: nextId.current++ }) as ChatItem)])
  }

  function send(raw: string, { echo = true } = {}) {
    const text = raw.trim().slice(0, MAX_LENGTH)
    if (!text || test.isPending) return
    if (echo) push({ kind: 'user', text, time: nowTime() })
    setDraft('')
    test.mutate(text, {
      onSuccess: (res) => {
        if (res.reply) push({ kind: 'bot', text: res.reply, time: nowTime() })
        else if (!res.botActive)
          push({
            kind: 'notice',
            tone: 'warning',
            text: 'Tu vendedor está pausado, así que no respondería a este mensaje. Actívalo en Reglas y políticas.',
          })
        else push({ kind: 'notice', tone: 'default', text: 'El vendedor no respondería a este mensaje.' })
        if (res.handoffTriggered)
          push({
            kind: 'notice',
            tone: 'info',
            text: 'Aquí la conversación pasaría a un asesor humano (y el bot se pausaría si así lo configuraste).',
          })
      },
      onError: (error) => {
        const rateLimited = error instanceof ApiError && error.status === 429
        push({
          kind: 'notice',
          tone: 'warning',
          text: rateLimited
            ? 'Vas muy rápido 😅 Hiciste muchas pruebas en poco tiempo. Espera un minuto y vuelve a intentarlo.'
            : `No se pudo obtener la respuesta: ${error.message}`,
          retry: text,
        })
      },
      onSettled: () => inputRef.current?.focus(),
    })
  }

  function onSubmit(e: FormEvent) {
    e.preventDefault()
    send(draft)
  }

  function onKeyDown(e: KeyboardEvent<HTMLTextAreaElement>) {
    if (e.key === 'Enter' && !e.shiftKey && !e.nativeEvent.isComposing) {
      e.preventDefault()
      send(draft)
    }
  }

  function clear() {
    setItems([])
    test.reset()
    inputRef.current?.focus()
  }

  return (
    <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_300px]">
      <div className="min-w-0 space-y-4">
        <Alert tone="info" title="Modo de prueba">
          Aquí no se crean pedidos, carritos ni conversaciones reales. Cada mensaje se evalúa por separado (el vendedor no
          recuerda los anteriores) y se usa la configuración <strong>guardada</strong>.
        </Alert>
        {isPaused && (
          <Alert tone="warning" title="Tu vendedor está pausado">
            No responderá en la prueba ni a tus clientes.{' '}
            <Link to="/seller/rules" className="text-primary font-medium hover:underline">
              Actívalo en Reglas y políticas
            </Link>
            .
          </Alert>
        )}

        <Card className="gap-0 overflow-hidden py-0">
          <ChatHeader
            name={vendorName}
            status={test.isPending ? 'escribiendo…' : isPaused ? 'pausado' : 'en línea'}
            actions={
              <Tooltip>
                <TooltipTrigger asChild>
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon-sm"
                    onClick={clear}
                    disabled={items.length === 0 || test.isPending}
                    aria-label="Limpiar conversación"
                  >
                    <Eraser />
                  </Button>
                </TooltipTrigger>
                <TooltipContent>Limpiar conversación</TooltipContent>
              </Tooltip>
            }
          />

          <ChatSurface className="h-[min(60vh,560px)] min-h-80">
            <div
              ref={scrollRef}
              className="h-full space-y-2 overflow-y-auto p-4"
              role="log"
              aria-live="polite"
              aria-label="Conversación de prueba"
            >
              {items.length === 0 ? (
                <EmptyChat name={vendorName} onPick={(s) => send(s)} disabled={test.isPending} />
              ) : (
                items.map((item) =>
                  item.kind === 'notice' ? (
                    <ChatNotice key={item.id} tone={item.tone}>
                      <span className="inline-flex flex-wrap items-center justify-center gap-x-2 gap-y-1">
                        {item.tone === 'info' && <ArrowRightLeft className="size-3.5" aria-hidden />}
                        {item.text}
                        {item.retry && (
                          <button
                            type="button"
                            onClick={() => send(item.retry ?? '', { echo: false })}
                            disabled={test.isPending}
                            className="text-primary inline-flex items-center gap-1 font-medium hover:underline disabled:opacity-50"
                          >
                            <RotateCcw className="size-3" aria-hidden /> Reintentar
                          </button>
                        )}
                      </span>
                    </ChatNotice>
                  ) : (
                    <ChatBubble key={item.id} side={item.kind === 'user' ? 'out' : 'in'} time={item.time}>
                      {item.text}
                    </ChatBubble>
                  ),
                )
              )}
              {test.isPending && <TypingIndicator name={vendorName} />}
            </div>
          </ChatSurface>

          <div className="bg-card space-y-3 border-t p-3">
            {items.length > 0 && (
              <div className="-mx-1 flex gap-2 overflow-x-auto px-1 pb-1" role="group" aria-label="Mensajes sugeridos">
                {SUGGESTIONS.map((s) => (
                  <SuggestionChip key={s} text={s} onPick={send} disabled={test.isPending} />
                ))}
              </div>
            )}
            <form onSubmit={onSubmit} className="flex items-end gap-2">
              <Textarea
                ref={inputRef}
                value={draft}
                onChange={(e) => setDraft(e.target.value)}
                onKeyDown={onKeyDown}
                rows={1}
                maxLength={MAX_LENGTH}
                placeholder="Escribe como si fueras un cliente…"
                aria-label="Mensaje de prueba"
                className="max-h-32 min-h-10 resize-none rounded-2xl"
              />
              <Button
                type="submit"
                size="icon-lg"
                className="bg-whatsapp hover:bg-whatsapp/90 shrink-0 rounded-full text-white"
                disabled={!draft.trim() || test.isPending}
                aria-label="Enviar mensaje"
              >
                <SendHorizontal />
              </Button>
            </form>
          </div>
        </Card>
      </div>

      <aside className="space-y-4" aria-label="Consejos de prueba">
        <Card className="gap-4">
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <Lightbulb className="text-warning size-4" aria-hidden /> Qué probar
            </CardTitle>
          </CardHeader>
          <CardContent>
            <ul className="text-muted-foreground space-y-2.5 text-sm">
              <li>
                <strong className="text-foreground">Saludo:</strong> comprueba el tono y el mensaje de bienvenida.
              </li>
              <li>
                <strong className="text-foreground">Productos:</strong> pregunta por precios, tallas o stock.
              </li>
              <li>
                <strong className="text-foreground">Comandos:</strong> <code className="text-xs">#catalogo</code>,{' '}
                <code className="text-xs">#ayuda</code>.
              </li>
              <li>
                <strong className="text-foreground">Negociación:</strong> pide descuento varias veces.
              </li>
              <li>
                <strong className="text-foreground">Derivación:</strong> escribe tu frase disparadora o pide un asesor.
              </li>
            </ul>
          </CardContent>
        </Card>
        <Card className="gap-3">
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <FlaskConical className="text-primary size-4" aria-hidden /> ¿No responde como esperas?
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-2 text-sm">
            <p className="text-muted-foreground">Ajusta la configuración, guarda y vuelve a probar.</p>
            <div className="flex flex-col gap-1.5">
              <Link to="/seller/personality" className="text-primary font-medium hover:underline">
                Personalidad →
              </Link>
              <Link to="/seller/rules" className="text-primary font-medium hover:underline">
                Reglas y políticas →
              </Link>
              <Link to="/seller/knowledge" className="text-primary font-medium hover:underline">
                Base de conocimiento →
              </Link>
            </div>
            <p className="text-muted-foreground pt-2 text-xs">Límite de pruebas: 20 mensajes por minuto.</p>
          </CardContent>
        </Card>
      </aside>
    </div>
  )
}

function SuggestionChip({ text, onPick, disabled }: { text: string; onPick: (t: string) => void; disabled?: boolean }) {
  return (
    <button
      type="button"
      onClick={() => onPick(text)}
      disabled={disabled}
      className={cn(
        'bg-background hover:bg-muted focus-visible:ring-ring/50 shrink-0 rounded-full border px-3 py-1 text-xs font-medium whitespace-nowrap transition-colors outline-none focus-visible:ring-2 disabled:opacity-50',
      )}
    >
      {text}
    </button>
  )
}

function EmptyChat({ name, onPick, disabled }: { name: string; onPick: (t: string) => void; disabled?: boolean }) {
  return (
    <div className="flex h-full flex-col items-center justify-center gap-4 px-2 py-6 text-center">
      <div className="bg-card rounded-xl px-4 py-3 shadow-xs">
        <p className="text-sm font-medium">Conversa con {name}</p>
        <p className="text-muted-foreground mt-0.5 text-xs">Escribe un mensaje o elige una sugerencia para empezar.</p>
      </div>
      <div className="flex max-w-md flex-wrap justify-center gap-2" role="group" aria-label="Mensajes sugeridos">
        {SUGGESTIONS.map((s) => (
          <SuggestionChip key={s} text={s} onPick={onPick} disabled={disabled} />
        ))}
      </div>
    </div>
  )
}
