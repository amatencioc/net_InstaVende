import type { ChannelStatus } from '../api'
import { cn } from '@/lib/utils'

const statusMeta: Record<ChannelStatus, { label: string; dot: string; className: string }> = {
  Connected: { label: 'Conectado', dot: 'bg-success', className: 'border-success/30 bg-success/10' },
  Disconnected: { label: 'Desconectado', dot: 'bg-muted-foreground', className: 'border-border bg-muted' },
  NeedsAttention: {
    label: 'Requiere atención',
    dot: 'bg-warning',
    className: 'border-warning/40 bg-warning/10',
  },
}

/** Insignia de estado del canal (texto + punto de color, legible en claro y oscuro). */
export function ChannelStatusBadge({ status, className }: { status: ChannelStatus; className?: string }) {
  const meta = statusMeta[status]
  return (
    <span
      className={cn(
        'text-foreground inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-xs font-medium whitespace-nowrap',
        meta.className,
        className,
      )}
    >
      <span
        className={cn(
          'size-1.5 rounded-full',
          meta.dot,
          status === 'Connected' && 'motion-safe:animate-pulse',
        )}
        aria-hidden
      />
      {meta.label}
    </span>
  )
}
