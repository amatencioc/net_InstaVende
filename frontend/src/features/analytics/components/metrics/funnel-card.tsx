import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { formatNumber, formatPercent } from '@/lib/format'
import type { MetricsData } from '../../api'

/** Opacidad decreciente del mismo tono (escala secuencial de un solo color). */
const stepShades = ['opacity-100', 'opacity-85', 'opacity-70', 'opacity-55', 'opacity-40']

/**
 * Embudo de conversión del periodo. Las barras son proporcionales al paso mayor y cada paso
 * posterior a "Conversaciones" muestra qué parte de las conversaciones llegó hasta él.
 */
export function FunnelCard({ funnel }: { funnel: MetricsData['funnel'] }) {
  const max = Math.max(1, ...funnel.map((s) => s.value))
  const conversations = funnel.find((s) => s.key === 'conversations')?.value ?? 0
  const conversationsIndex = funnel.findIndex((s) => s.key === 'conversations')
  const isEmpty = funnel.every((s) => s.value === 0)

  return (
    <Card className="gap-4">
      <CardHeader>
        <CardTitle>Embudo de conversión</CardTitle>
        <CardDescription>
          De conversación a pedido entregado, sobre las conversaciones iniciadas en el periodo
        </CardDescription>
      </CardHeader>
      <CardContent>
        {isEmpty ? (
          <p className="text-muted-foreground rounded-xl border border-dashed px-4 py-10 text-center text-sm">
            Aún no hay conversaciones en este periodo. El embudo se llenará cuando tus clientes empiecen a
            escribir.
          </p>
        ) : (
          <ol className="space-y-3">
            {funnel.map((step, i) => {
              const width = Math.max(2, (step.value / max) * 100)
              const share =
                i > conversationsIndex && conversations > 0 ? (step.value / conversations) * 100 : null
              const previous = i > 0 ? funnel[i - 1] : undefined
              const stepRate =
                previous && previous.value > 0 && i > conversationsIndex + 1
                  ? (step.value / previous.value) * 100
                  : null
              return (
                <li key={step.key} className="space-y-1.5">
                  <div className="flex items-baseline justify-between gap-3 text-sm">
                    <span className="font-medium">{step.label}</span>
                    <span className="flex items-baseline gap-2">
                      <span className="font-semibold tabular-nums">{formatNumber(step.value)}</span>
                      {share !== null && (
                        <span className="text-muted-foreground w-14 text-right text-xs tabular-nums">
                          {formatPercent(share)}
                        </span>
                      )}
                    </span>
                  </div>
                  <div className="bg-muted h-3 overflow-hidden rounded-full" aria-hidden>
                    <div
                      className={`bg-primary h-full rounded-full transition-[width] duration-500 ${stepShades[i] ?? 'opacity-40'}`}
                      style={{ width: `${width}%` }}
                    />
                  </div>
                  {stepRate !== null && (
                    <p className="text-muted-foreground text-xs">
                      {formatPercent(stepRate)} de «{previous!.label.toLowerCase()}» avanzó a este paso
                    </p>
                  )}
                </li>
              )
            })}
          </ol>
        )}
      </CardContent>
    </Card>
  )
}
