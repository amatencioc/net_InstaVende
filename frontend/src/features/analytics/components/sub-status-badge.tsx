import { cn } from '@/lib/utils'
import { subStatusTone } from '../utils'

/** Badge del sub-estado de un pedido con la etiqueta y el tono que define OrderWorkflow. */
export function SubStatusBadge({
  label,
  css,
  className,
}: {
  label: string
  css: string
  className?: string
}) {
  return (
    <span
      className={cn(
        'inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium whitespace-nowrap',
        subStatusTone(css).badge,
        className,
      )}
    >
      {label}
    </span>
  )
}
