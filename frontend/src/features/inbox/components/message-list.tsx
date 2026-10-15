import { useInfiniteQuery } from '@tanstack/react-query'
import { format, isThisYear, isToday, isYesterday } from 'date-fns'
import { es } from 'date-fns/locale'
import {
  ArrowDown,
  Bot,
  Check,
  CircleAlert,
  Clock,
  FileText,
  Loader2,
  MessageSquareDashed,
  RotateCcw,
} from 'lucide-react'
import { memo, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react'
import { flattenMessages, messagesQuery, type ChatMessage } from '../api'
import { QueryError } from '@/components/shared/query-state'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'

const GROUP_WINDOW_MS = 5 * 60_000
const BOTTOM_THRESHOLD_PX = 80

function dayKey(iso: string) {
  return format(new Date(iso), 'yyyy-MM-dd')
}

function dayLabel(iso: string) {
  const d = new Date(iso)
  if (isToday(d)) return 'Hoy'
  if (isYesterday(d)) return 'Ayer'
  return format(d, isThisYear(d) ? "EEEE d 'de' MMMM" : "d 'de' MMMM 'de' yyyy", { locale: es })
}

function sameSender(a: ChatMessage, b: ChatMessage) {
  return a.direction === b.direction && a.sentByBot === b.sentByBot
}

type Row =
  | { kind: 'day'; key: string; label: string }
  | { kind: 'message'; key: string; message: ChatMessage; groupStart: boolean; groupEnd: boolean }

function buildRows(messages: ChatMessage[]): Row[] {
  const rows: Row[] = []
  let lastDay = ''
  messages.forEach((m, i) => {
    const day = dayKey(m.sentAt)
    if (day !== lastDay) {
      rows.push({ kind: 'day', key: `day-${day}`, label: dayLabel(m.sentAt) })
      lastDay = day
    }
    const prev = messages[i - 1]
    const next = messages[i + 1]
    const t = new Date(m.sentAt).getTime()
    const groupStart =
      !prev ||
      !sameSender(prev, m) ||
      dayKey(prev.sentAt) !== day ||
      t - new Date(prev.sentAt).getTime() > GROUP_WINDOW_MS
    const groupEnd =
      !next ||
      !sameSender(next, m) ||
      dayKey(next.sentAt) !== day ||
      new Date(next.sentAt).getTime() - t > GROUP_WINDOW_MS
    rows.push({ kind: 'message', key: m.clientId ?? String(m.id), message: m, groupStart, groupEnd })
  })
  return rows
}

/**
 * Historial con scroll infinito hacia arriba: al llegar arriba se cargan mensajes anteriores
 * manteniendo la posición. Los mensajes nuevos bajan el scroll solo si ya se estaba al final.
 */
export function MessageList({
  conversationId,
  onRetry,
}: {
  conversationId: number
  onRetry: (message: ChatMessage) => void
}) {
  const query = useInfiniteQuery(messagesQuery(conversationId))
  const messages = useMemo(() => flattenMessages(query.data), [query.data])
  const rows = useMemo(() => buildRows(messages), [messages])

  const scrollRef = useRef<HTMLDivElement>(null)
  const topRef = useRef<HTMLDivElement>(null)
  const heightBeforeFetch = useRef<number | null>(null)
  const initialized = useRef(false)
  const atBottomRef = useRef(true)
  const [atBottom, setAtBottom] = useState(true)

  const pageCount = query.data?.pages.length ?? 0
  const last = messages.at(-1)
  const lastKey = last ? (last.clientId ?? last.id) : null

  // Carga de mensajes anteriores al acercarse al principio
  const { hasNextPage, isFetchingNextPage, fetchNextPage } = query
  useEffect(() => {
    const el = topRef.current
    const root = scrollRef.current
    if (!el || !root || !hasNextPage) return
    const io = new IntersectionObserver(
      ([entry]) => {
        if (entry?.isIntersecting && !isFetchingNextPage && initialized.current) {
          heightBeforeFetch.current = root.scrollHeight
          void fetchNextPage()
        }
      },
      { root, rootMargin: '300px 0px 0px 0px' },
    )
    io.observe(el)
    return () => io.disconnect()
  }, [hasNextPage, isFetchingNextPage, fetchNextPage])

  // Mantiene la posición visual al anteponer una página antigua
  useLayoutEffect(() => {
    const root = scrollRef.current
    if (!root || heightBeforeFetch.current === null) return
    root.scrollTop += root.scrollHeight - heightBeforeFetch.current
    heightBeforeFetch.current = null
  }, [pageCount])

  // Primer render al final; después, seguir el final solo si el usuario ya estaba ahí o si envió él
  const lastIsOwn = last?.direction === 'Outbound' && !last.sentByBot && last.clientId !== undefined
  useLayoutEffect(() => {
    const root = scrollRef.current
    if (!root || lastKey === null) return
    if (!initialized.current) {
      root.scrollTop = root.scrollHeight
      initialized.current = true
      return
    }
    if (atBottomRef.current || lastIsOwn) root.scrollTo({ top: root.scrollHeight, behavior: 'smooth' })
  }, [lastKey, lastIsOwn])

  // Mensajes entrantes llegados mientras el usuario lee más arriba
  let lastInboundId: number | null = null
  for (let i = messages.length - 1; i >= 0 && lastInboundId === null; i--) {
    const m = messages[i]
    if (m?.direction === 'Inbound') lastInboundId = m.id
  }
  const [seenInboundId, setSeenInboundId] = useState<number | null>(null)
  const unseen =
    atBottom || seenInboundId === null
      ? 0
      : messages.filter((m) => m.direction === 'Inbound' && m.id > seenInboundId).length

  const onScroll = () => {
    const root = scrollRef.current
    if (!root) return
    const bottom = root.scrollHeight - root.scrollTop - root.clientHeight < BOTTOM_THRESHOLD_PX
    atBottomRef.current = bottom
    setAtBottom(bottom)
    if (bottom) setSeenInboundId(lastInboundId)
  }

  const scrollToBottom = () => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight, behavior: 'smooth' })
  }

  if (query.isPending) return <MessagesSkeleton />
  if (query.isError)
    return (
      <div className="p-4">
        <QueryError error={query.error} onRetry={() => void query.refetch()} />
      </div>
    )

  return (
    <div className="relative min-h-0 flex-1">
      <div
        ref={scrollRef}
        onScroll={onScroll}
        className="bg-muted/30 h-full overflow-y-auto overscroll-contain px-3 py-4 sm:px-6"
        role="log"
        aria-live="polite"
        aria-relevant="additions"
        aria-label="Mensajes"
        tabIndex={0}
      >
        <div ref={topRef} aria-hidden className="h-px" />
        {isFetchingNextPage && (
          <div
            className="text-muted-foreground flex items-center justify-center gap-2 py-2 text-xs"
            role="status"
          >
            <Loader2 className="size-4 animate-spin" aria-hidden /> Cargando mensajes anteriores…
          </div>
        )}
        {!hasNextPage && messages.length > 0 && (
          <p className="text-muted-foreground py-2 text-center text-xs">Inicio de la conversación</p>
        )}
        {messages.length === 0 ? (
          <div className="text-muted-foreground flex h-full flex-col items-center justify-center gap-2 text-center text-sm">
            <MessageSquareDashed className="size-6" aria-hidden />
            Todavía no hay mensajes en esta conversación.
          </div>
        ) : (
          <ol className="mx-auto max-w-3xl">
            {rows.map((row) =>
              row.kind === 'day' ? (
                <li key={row.key} className="sticky top-0 z-10 flex justify-center py-3">
                  <span className="bg-background/90 text-muted-foreground rounded-full border px-3 py-1 text-xs font-medium capitalize shadow-xs backdrop-blur">
                    {row.label}
                  </span>
                </li>
              ) : (
                <MessageBubble
                  key={row.key}
                  message={row.message}
                  groupStart={row.groupStart}
                  groupEnd={row.groupEnd}
                  onRetry={onRetry}
                />
              ),
            )}
          </ol>
        )}
      </div>
      {!atBottom && (
        <Button
          size="sm"
          variant={unseen > 0 ? 'default' : 'outline'}
          className="absolute right-4 bottom-4 rounded-full shadow-md"
          onClick={scrollToBottom}
          aria-label={unseen > 0 ? `${unseen} mensajes nuevos, ir al final` : 'Ir al último mensaje'}
        >
          <ArrowDown />
          {unseen > 0 && <span>{unseen > 9 ? '9+' : unseen} nuevos</span>}
        </Button>
      )}
    </div>
  )
}

const MessageBubble = memo(function MessageBubble({
  message: m,
  groupStart,
  groupEnd,
  onRetry,
}: {
  message: ChatMessage
  groupStart: boolean
  groupEnd: boolean
  onRetry: (message: ChatMessage) => void
}) {
  const outbound = m.direction === 'Outbound'
  const bot = outbound && m.sentByBot
  const failed = m.deliveryStatus === 'Failed'
  const time = format(new Date(m.sentAt), 'HH:mm')
  const isImage =
    !!m.mediaUrl && (m.mediaType?.startsWith('image') ?? /\.(png|jpe?g|gif|webp)$/i.test(m.mediaUrl))

  return (
    <li
      className={cn('flex flex-col', outbound ? 'items-end' : 'items-start', groupStart ? 'mt-3' : 'mt-0.5')}
    >
      {groupStart && bot && (
        <span className="text-brand-accent mb-1 inline-flex items-center gap-1 px-1 text-[11px] font-medium">
          <Bot className="size-3" aria-hidden /> Vendedor IA
        </span>
      )}
      <div
        className={cn(
          'max-w-[85%] rounded-2xl px-3.5 py-2 text-sm shadow-xs sm:max-w-[70%]',
          !outbound && 'bg-card border',
          outbound && !bot && 'bg-primary text-primary-foreground',
          bot && 'bg-accent text-accent-foreground',
          groupEnd && (outbound ? 'rounded-br-md' : 'rounded-bl-md'),
          failed && 'ring-destructive/60 ring-2',
        )}
      >
        {m.mediaUrl &&
          (isImage ? (
            <a href={m.mediaUrl} target="_blank" rel="noreferrer noopener" className="-mx-1.5 mb-1.5 block">
              <img
                src={m.mediaUrl}
                alt="Imagen adjunta"
                loading="lazy"
                className="max-h-72 rounded-xl object-cover"
              />
            </a>
          ) : (
            <a
              href={m.mediaUrl}
              target="_blank"
              rel="noreferrer noopener"
              className="mb-1.5 flex items-center gap-2 underline underline-offset-2"
            >
              <FileText className="size-4" aria-hidden /> Ver archivo adjunto
            </a>
          ))}
        {m.content && <p className="break-words whitespace-pre-wrap">{m.content}</p>}
        <div
          className={cn(
            'mt-1 flex items-center justify-end gap-1 text-[11px] leading-none',
            outbound && !bot ? 'text-primary-foreground/75' : 'text-muted-foreground',
          )}
        >
          <time dateTime={m.sentAt}>{time}</time>
          {outbound && <DeliveryIcon status={m.deliveryStatus} />}
        </div>
      </div>
      {failed && (
        <div className="text-destructive mt-1 flex items-center gap-2 px-1 text-xs" role="alert">
          <span>{m.localError ?? 'No se pudo entregar.'}</span>
          <button
            type="button"
            onClick={() => onRetry(m)}
            className="focus-visible:ring-ring/50 inline-flex items-center gap-1 rounded font-medium underline-offset-2 outline-none hover:underline focus-visible:ring-[3px]"
          >
            <RotateCcw className="size-3" aria-hidden /> Reintentar
          </button>
        </div>
      )}
    </li>
  )
})

function DeliveryIcon({ status }: { status: ChatMessage['deliveryStatus'] }) {
  if (status === 'Pending')
    return (
      <span title="Enviando">
        <Clock className="size-3" aria-hidden />
        <span className="sr-only">Enviando</span>
      </span>
    )
  if (status === 'Failed')
    return (
      <span title="No entregado">
        <CircleAlert className="size-3" aria-hidden />
        <span className="sr-only">No entregado</span>
      </span>
    )
  return (
    <span title="Enviado">
      <Check className="size-3" aria-hidden />
      <span className="sr-only">Enviado</span>
    </span>
  )
}

function MessagesSkeleton() {
  const widths = ['w-2/5', 'w-1/2', 'w-1/3', 'w-3/5', 'w-1/4', 'w-2/5']
  return (
    <div
      className="bg-muted/30 min-h-0 flex-1 space-y-3 overflow-hidden px-3 py-6 sm:px-6"
      role="status"
      aria-label="Cargando mensajes"
    >
      {widths.map((w, i) => (
        <div key={i} className={cn('flex', i % 2 ? 'justify-end' : 'justify-start')}>
          <Skeleton className={cn('h-10 rounded-2xl', w)} />
        </div>
      ))}
    </div>
  )
}
