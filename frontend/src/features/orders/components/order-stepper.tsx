import { Check, XCircle } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { OrderSubStatus, OrderWorkflow } from '../api'

/**
 * Progreso visual del pedido a lo largo del flujo principal (todas las columnas
 * salvo la pestaña de cancelados, en el orden definido por OrderWorkflow).
 */
export function OrderStepper({
  workflow,
  current,
  currentLabel,
}: {
  workflow: OrderWorkflow
  current: OrderSubStatus
  currentLabel: string
}) {
  const steps = workflow.tabs.filter((t) => t.tab !== 'Cancelados').flatMap((t) => t.columns)
  const index = steps.findIndex((s) => s.subStatus === current)

  if (index === -1) {
    return (
      <div className="border-destructive/30 bg-destructive/5 flex items-center gap-3 rounded-lg border p-3 text-sm">
        <XCircle className="text-destructive size-5 shrink-0" aria-hidden />
        <p>
          Este pedido está <strong className="font-semibold">{currentLabel.toLowerCase()}</strong> y ya no forma parte
          del flujo de entrega. Puedes reactivarlo con «Cambiar estado».
        </p>
      </div>
    )
  }

  return (
    <ol aria-label="Progreso del pedido" className="flex flex-col gap-3 md:flex-row md:gap-0">
      {steps.map((step, i) => {
        const done = i < index
        const active = i === index
        return (
          <li
            key={step.subStatus}
            aria-current={active ? 'step' : undefined}
            className="relative flex items-center gap-3 md:flex-1 md:flex-col md:gap-2 md:text-center"
          >
            {/* Conector con el paso anterior */}
            {i > 0 && (
              <span
                aria-hidden
                className={cn(
                  'absolute hidden h-0.5 md:top-4 md:right-1/2 md:block md:w-full',
                  i <= index ? 'bg-primary' : 'bg-border',
                )}
              />
            )}
            <span
              className={cn(
                'relative z-10 flex size-8 shrink-0 items-center justify-center rounded-full border-2 text-xs font-semibold',
                done && 'border-primary bg-primary text-primary-foreground',
                active && 'border-primary bg-background text-primary ring-primary/20 ring-4',
                !done && !active && 'border-border bg-background text-muted-foreground',
              )}
            >
              {done ? <Check className="size-4" aria-hidden /> : i + 1}
            </span>
            <span
              className={cn(
                'text-sm md:text-xs',
                active ? 'text-foreground font-semibold' : done ? 'text-foreground' : 'text-muted-foreground',
              )}
            >
              {step.label}
              <span className="sr-only">{done ? ' (completado)' : active ? ' (estado actual)' : ' (pendiente)'}</span>
            </span>
          </li>
        )
      })}
    </ol>
  )
}
