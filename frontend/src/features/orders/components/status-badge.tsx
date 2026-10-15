import { cn } from '@/lib/utils'
import { statusTone } from '../status'

/** Badge del sub-estado con el color definido por OrderWorkflow. */
export function OrderStatusBadge({ label, css, className }: { label: string; css: string; className?: string }) {
  const tone = statusTone(css)
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-xs font-medium whitespace-nowrap',
        tone.badge,
        className,
      )}
    >
      <span className={cn('size-1.5 rounded-full', tone.dot)} aria-hidden />
      {label}
    </span>
  )
}

/** Punto de color del sub-estado (cabeceras de columna, menús). */
export function StatusDot({ css, className }: { css: string; className?: string }) {
  return <span className={cn('inline-block size-2 shrink-0 rounded-full', statusTone(css).dot, className)} aria-hidden />
}
