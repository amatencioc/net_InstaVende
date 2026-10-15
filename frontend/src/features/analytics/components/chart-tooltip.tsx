import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

interface TooltipItem {
  name?: string | number
  value?: unknown
  color?: string
  dataKey?: unknown
  payload?: unknown
}

interface AnalyticsTooltipProps {
  active?: boolean
  payload?: ReadonlyArray<TooltipItem>
  label?: string | number
  /** Etiqueta de cabecera (p.ej. fecha formateada). */
  formatLabel?: (label: string) => ReactNode
  /** Nombre visible de cada serie por su dataKey. */
  names?: Record<string, string>
  /** Formato del valor por dataKey. */
  formatValue?: (value: number, key: string) => string
  /** Muestra además el total de las series (barras apiladas). */
  showTotal?: boolean
}

/**
 * Tooltip de los gráficos de analítica: cabecera con la fecha, una fila por serie con su
 * marca de color y el valor formateado con tokens de texto (nunca el color de la serie).
 */
export function AnalyticsTooltip({
  active,
  payload,
  label,
  formatLabel,
  names,
  formatValue,
  showTotal,
}: AnalyticsTooltipProps) {
  if (!active || !payload?.length) return null
  const rows = payload.filter((p) => typeof p.value === 'number')
  const total = rows.reduce((sum, p) => sum + (p.value as number), 0)
  const fmt = (value: number, key: string) =>
    formatValue ? formatValue(value, key) : value.toLocaleString('es-PE')

  return (
    <div className="border-border/60 bg-popover text-popover-foreground grid min-w-40 gap-1.5 rounded-lg border px-3 py-2 text-xs shadow-xl">
      {label != null && <div className="font-medium">{formatLabel ? formatLabel(String(label)) : label}</div>}
      <div className="grid gap-1">
        {rows.map((item) => {
          const key = String(
            item.dataKey === 'value' ? (item.name ?? 'value') : (item.dataKey ?? item.name ?? ''),
          )
          const color = item.color ?? (item.payload as { fill?: string } | undefined)?.fill
          return (
            <div key={key} className="flex items-center gap-2">
              <span
                aria-hidden
                className="size-2.5 shrink-0 rounded-[3px]"
                style={{ background: color ?? 'var(--muted-foreground)' }}
              />
              <span className="text-muted-foreground">{names?.[key] ?? item.name}</span>
              <span className="ml-auto pl-3 font-medium tabular-nums">{fmt(item.value as number, key)}</span>
            </div>
          )
        })}
        {showTotal && rows.length > 1 && (
          <div className={cn('mt-0.5 flex items-center gap-2 border-t pt-1')}>
            <span className="text-muted-foreground">Total</span>
            <span className="ml-auto pl-3 font-medium tabular-nums">{fmt(total, 'total')}</span>
          </div>
        )}
      </div>
    </div>
  )
}

/** Aviso centrado sobre un gráfico sin datos (mantiene la estructura visible). */
export function ChartEmptyOverlay({ title, description }: { title: string; description?: ReactNode }) {
  return (
    <div className="pointer-events-none absolute inset-0 flex items-center justify-center p-4">
      <div className="bg-card/90 max-w-xs rounded-lg border px-4 py-3 text-center shadow-sm backdrop-blur-sm">
        <p className="text-sm font-medium">{title}</p>
        {description && <p className="text-muted-foreground mt-0.5 text-xs">{description}</p>}
      </div>
    </div>
  )
}
