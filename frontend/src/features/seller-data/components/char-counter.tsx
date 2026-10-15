import { cn } from '@/lib/utils'

/** Contador de caracteres para campos con límite; se resalta al acercarse o superar el máximo. */
export function CharCounter({ value, max, className }: { value: string; max: number; className?: string }) {
  const length = value.length
  const ratio = length / max
  return (
    <span
      className={cn(
        'text-muted-foreground text-xs tabular-nums',
        ratio >= 0.9 && 'text-warning',
        length > max && 'text-destructive font-medium',
        className,
      )}
    >
      {length.toLocaleString('es-PE')}/{max.toLocaleString('es-PE')}
      <span className="sr-only"> caracteres</span>
    </span>
  )
}
