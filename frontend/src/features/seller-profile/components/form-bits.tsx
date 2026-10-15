import type { LucideIcon } from 'lucide-react'
import { useId, type ComponentProps, type ReactNode } from 'react'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'

/** Tarjeta de sección del formulario con icono, título, descripción y acción opcional. */
export function SectionCard({
  icon: Icon,
  title,
  description,
  action,
  children,
  className,
  id,
}: {
  icon: LucideIcon
  title: string
  description?: ReactNode
  action?: ReactNode
  children: ReactNode
  className?: string
  id?: string
}) {
  const titleId = useId()
  return (
    <Card className={cn('scroll-mt-20 gap-5', className)} aria-labelledby={titleId} role="region" id={id}>
      <CardHeader className="flex flex-row items-start gap-3">
        <div className="bg-primary/10 text-primary flex size-9 shrink-0 items-center justify-center rounded-lg">
          <Icon className="size-4.5" aria-hidden />
        </div>
        <div className="min-w-0 flex-1 space-y-1">
          <CardTitle id={titleId} className="font-display text-base">
            {title}
          </CardTitle>
          {description && <CardDescription>{description}</CardDescription>}
        </div>
        {action && <div className="shrink-0">{action}</div>}
      </CardHeader>
      <CardContent className="space-y-5">{children}</CardContent>
    </Card>
  )
}

/** Contador "12/200" que cambia de color al acercarse al límite. */
export function CharCount({ value, max, className }: { value: string; max: number; className?: string }) {
  const length = value.length
  const ratio = length / max
  return (
    <span
      className={cn(
        'text-muted-foreground text-xs tabular-nums',
        ratio >= 0.9 && 'text-warning',
        ratio >= 1 && 'text-destructive',
        className,
      )}
      aria-live="polite"
      aria-label={`${length} de ${max} caracteres`}
    >
      {length}/{max}
    </span>
  )
}

/** Fila con etiqueta a la izquierda y un elemento (contador, ayuda) a la derecha. */
export function LabelRow({ children, aside }: { children: ReactNode; aside?: ReactNode }) {
  return (
    <div className="flex items-end justify-between gap-2">
      {children}
      {aside}
    </div>
  )
}

/** Input numérico con sufijo o prefijo visual (%, S/). Devuelve NaN si está vacío (lo rechaza Zod). */
export function AffixNumberInput({
  value,
  onChange,
  prefix,
  suffix,
  className,
  ...props
}: Omit<ComponentProps<'input'>, 'value' | 'onChange' | 'prefix'> & {
  value: number
  onChange: (value: number) => void
  prefix?: string
  suffix?: string
}) {
  return (
    <div className={cn('relative', className)}>
      {prefix && (
        <span className="text-muted-foreground pointer-events-none absolute inset-y-0 left-3 flex items-center text-sm">
          {prefix}
        </span>
      )}
      <Input
        type="number"
        inputMode="decimal"
        value={Number.isNaN(value) ? '' : value}
        onChange={(e) => onChange(e.target.valueAsNumber)}
        className={cn('tabular-nums', prefix && 'pl-11', suffix && 'pr-8')}
        {...props}
      />
      {suffix && (
        <span className="text-muted-foreground pointer-events-none absolute inset-y-0 right-3 flex items-center text-sm">
          {suffix}
        </span>
      )}
    </div>
  )
}

/** Esqueleto de una página de formulario por tarjetas mientras carga. */
export function FormPageSkeleton({ cards = 3, aside = false }: { cards?: number; aside?: boolean }) {
  return (
    <div className={cn('grid gap-6', aside && 'lg:grid-cols-[minmax(0,1fr)_340px]')} aria-busy="true">
      <span className="sr-only">Cargando…</span>
      <div className="space-y-6">
        {Array.from({ length: cards }, (_, i) => (
          <div key={i} className="bg-card space-y-5 rounded-xl border p-6">
            <div className="flex items-center gap-3">
              <Skeleton className="size-9 rounded-lg" />
              <div className="space-y-2">
                <Skeleton className="h-4 w-40" />
                <Skeleton className="h-3 w-64" />
              </div>
            </div>
            <div className="grid gap-4 sm:grid-cols-2">
              <Skeleton className="h-9" />
              <Skeleton className="h-9" />
            </div>
            <Skeleton className="h-20" />
          </div>
        ))}
      </div>
      {aside && <Skeleton className="hidden h-[420px] rounded-xl lg:block" />}
    </div>
  )
}
