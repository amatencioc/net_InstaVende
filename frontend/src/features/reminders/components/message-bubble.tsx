import { CheckCheck } from 'lucide-react'
import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

/** Fondo de chat estilo WhatsApp (patrón sutil con los tokens del tema). */
export function ChatSurface({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <div
      className={cn(
        'bg-muted/60 flex flex-col gap-3 rounded-xl p-4',
        'bg-[radial-gradient(var(--border)_1px,transparent_1px)] [background-size:14px_14px]',
        className,
      )}
    >
      {children}
    </div>
  )
}

interface MessageBubbleProps {
  message: string
  /** Hora simulada que se muestra en la burbuja ("18:20"). */
  time?: string
  placeholder?: string
  className?: string
}

/** Burbuja de mensaje saliente con cola, hora y doble check. */
export function MessageBubble({
  message,
  time = '18:20',
  placeholder = 'Tu mensaje aparecerá aquí…',
  className,
}: MessageBubbleProps) {
  const empty = message.trim().length === 0
  return (
    <div className={cn('flex justify-end', className)}>
      <div
        className={cn(
          'relative max-w-[85%] rounded-lg rounded-tr-none px-3 pt-2 pb-1.5 text-sm shadow-xs',
          'bg-primary/15 text-foreground dark:bg-primary/25',
          // cola de la burbuja
          "after:absolute after:top-0 after:-right-2 after:size-0 after:border-t-8 after:border-r-8 after:border-r-transparent after:content-['']",
          'after:border-t-primary/15 dark:after:border-t-primary/25',
        )}
      >
        <p className={cn('break-words whitespace-pre-wrap', empty && 'text-muted-foreground italic')}>
          {empty ? placeholder : message}
        </p>
        <span className="text-muted-foreground mt-0.5 flex items-center justify-end gap-1 text-[11px] tabular-nums">
          {time}
          <CheckCheck className="text-info size-3.5" aria-hidden />
        </span>
      </div>
    </div>
  )
}
