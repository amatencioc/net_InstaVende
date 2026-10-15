import { CheckCheck } from 'lucide-react'
import type { ReactNode } from 'react'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { initials } from '@/lib/format'
import { cn } from '@/lib/utils'

/**
 * Fondo de chat al estilo WhatsApp. Usa tokens del tema (muted / foreground) para que el patrón
 * funcione igual en modo claro y oscuro.
 */
export function ChatSurface({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <div
      className={cn('bg-muted/60 dark:bg-background/60 relative', className)}
      style={{
        backgroundImage:
          'radial-gradient(color-mix(in oklab, var(--foreground) 7%, transparent) 1px, transparent 1px)',
        backgroundSize: '18px 18px',
      }}
    >
      {children}
    </div>
  )
}

/** Cabecera de la conversación (avatar + nombre + estado). */
export function ChatHeader({
  name,
  status,
  actions,
  className,
}: {
  name: string
  status?: ReactNode
  actions?: ReactNode
  className?: string
}) {
  return (
    <div className={cn('bg-card flex items-center gap-3 border-b px-4 py-3', className)}>
      <Avatar className="size-9">
        <AvatarFallback className="bg-whatsapp/15 text-whatsapp text-sm font-semibold">{initials(name)}</AvatarFallback>
      </Avatar>
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm leading-tight font-semibold">{name}</p>
        {status && <div className="text-muted-foreground truncate text-xs">{status}</div>}
      </div>
      {actions}
    </div>
  )
}

/**
 * Burbuja de mensaje. `side="out"` es el mensaje del cliente (derecha, verde),
 * `side="in"` el del vendedor (izquierda, tarjeta).
 */
export function ChatBubble({
  side,
  children,
  time,
  className,
}: {
  side: 'in' | 'out'
  children: ReactNode
  time?: string
  className?: string
}) {
  return (
    <div className={cn('flex', side === 'out' ? 'justify-end' : 'justify-start')}>
      <div
        className={cn(
          'relative max-w-[85%] rounded-2xl px-3 py-2 text-sm shadow-xs sm:max-w-[75%]',
          side === 'out'
            ? 'bg-whatsapp/20 dark:bg-whatsapp/25 text-foreground rounded-tr-sm'
            : 'bg-card text-card-foreground rounded-tl-sm',
          className,
        )}
      >
        <div className="break-words whitespace-pre-wrap">{children}</div>
        {time && (
          <div className="text-muted-foreground mt-0.5 flex items-center justify-end gap-1 text-[10px] leading-none">
            {time}
            {side === 'out' && <CheckCheck className="text-info size-3" aria-hidden />}
          </div>
        )}
      </div>
    </div>
  )
}

/** Indicador animado "escribiendo…". */
export function TypingIndicator({ name }: { name: string }) {
  return (
    <div className="flex justify-start" role="status" aria-live="polite">
      <div className="bg-card flex items-center gap-1 rounded-2xl rounded-tl-sm px-3 py-2.5 shadow-xs">
        <span className="sr-only">{name} está escribiendo…</span>
        {[0, 150, 300].map((delay) => (
          <span
            key={delay}
            className="bg-muted-foreground/60 size-1.5 animate-bounce rounded-full"
            style={{ animationDelay: `${delay}ms` }}
            aria-hidden
          />
        ))}
      </div>
    </div>
  )
}

/** Aviso centrado dentro del chat (sistema). */
export function ChatNotice({ children, tone = 'default' }: { children: ReactNode; tone?: 'default' | 'warning' | 'info' }) {
  return (
    <div className="flex justify-center">
      <div
        className={cn(
          'max-w-[90%] rounded-lg px-3 py-1.5 text-center text-xs shadow-xs',
          tone === 'default' && 'bg-card text-muted-foreground',
          tone === 'warning' && 'bg-warning/15 text-foreground',
          tone === 'info' && 'bg-info/10 text-foreground',
        )}
      >
        {children}
      </div>
    </div>
  )
}
