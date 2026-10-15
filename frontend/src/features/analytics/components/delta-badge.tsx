import { ArrowDownRight, ArrowUpRight, Minus } from 'lucide-react'
import { cn } from '@/lib/utils'
import { percentChange } from '../utils'

interface DeltaBadgeProps {
  current: number
  previous: number
  /** true cuando un valor menor es mejor (p.ej. tiempo de respuesta). */
  lowerIsBetter?: boolean
  className?: string
}

/** Variación compacta frente al periodo anterior, con flecha y texto accesible. */
export function DeltaBadge({ current, previous, lowerIsBetter, className }: DeltaBadgeProps) {
  const change = percentChange(current, previous)
  if (change === null)
    return <span className={cn('text-muted-foreground text-xs', className)}>sin datos previos</span>

  const rounded = Math.round(change * 10) / 10
  const good = lowerIsBetter ? rounded < 0 : rounded > 0
  const Icon = rounded === 0 ? Minus : rounded > 0 ? ArrowUpRight : ArrowDownRight
  return (
    <span
      className={cn(
        'inline-flex items-center gap-0.5 text-xs font-medium tabular-nums',
        rounded === 0 ? 'text-muted-foreground' : good ? 'text-success' : 'text-destructive',
        className,
      )}
      title="Frente al periodo anterior"
    >
      <Icon className="size-3.5" aria-hidden />
      {Math.abs(rounded).toFixed(1)}%
      <span className="sr-only">
        {rounded === 0 ? ' sin cambios' : rounded > 0 ? ' más' : ' menos'} que el periodo anterior
      </span>
    </span>
  )
}
