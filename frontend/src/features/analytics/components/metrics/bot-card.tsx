import { Info } from 'lucide-react'
import type { ReactNode } from 'react'
import { Bar, BarChart, CartesianGrid, XAxis, YAxis } from 'recharts'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import {
  ChartContainer,
  ChartLegend,
  ChartLegendContent,
  ChartTooltip,
  type ChartConfig,
} from '@/components/ui/chart'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { formatNumber, formatPercent } from '@/lib/format'
import type { MetricsData } from '../../api'
import { capitalize, formatDayLong, formatDayShort, formatDuration } from '../../utils'
import { AnalyticsTooltip, ChartEmptyOverlay } from '../chart-tooltip'
import { DeltaBadge } from '../delta-badge'

const chartConfig = {
  botMessages: { label: 'Bot', color: 'var(--chart-2)' },
  humanMessages: { label: 'Equipo', color: 'var(--chart-1)' },
} satisfies ChartConfig

interface BotCardProps {
  bot: MetricsData['bot']
  previous: MetricsData['previousBot']
  daily: MetricsData['daily']
}

/** Rendimiento del vendedor IA: automatización, derivaciones, primera respuesta y mensajes por día. */
export function BotCard({ bot, previous, daily }: BotCardProps) {
  const totalMessages = bot.botMessages + bot.humanMessages
  const summary =
    totalMessages === 0
      ? 'Sin mensajes enviados en el periodo.'
      : `Mensajes enviados en el periodo: ${formatNumber(bot.botMessages)} por el bot y ${formatNumber(bot.humanMessages)} por tu equipo. ` +
        `Automatización del ${formatPercent(bot.automationRate)}.`

  return (
    <Card className="gap-4">
      <CardHeader>
        <CardTitle>Rendimiento del vendedor IA</CardTitle>
        <CardDescription>Mensajes enviados por el bot frente a los escritos por tu equipo</CardDescription>
      </CardHeader>
      <CardContent className="space-y-5">
        <dl className="grid grid-cols-3 gap-3">
          <Stat
            label="Automatización"
            value={formatPercent(bot.automationRate)}
            delta={<DeltaBadge current={bot.automationRate} previous={previous.automationRate} />}
            help="Porcentaje de mensajes salientes enviados automáticamente por el bot (incluye recordatorios)."
          />
          <Stat
            label="Derivaciones"
            value={formatNumber(bot.handoffs)}
            sub={
              bot.conversations > 0 ? `${formatPercent(bot.handoffRate)} de las conversaciones` : undefined
            }
            delta={<DeltaBadge current={bot.handoffRate} previous={previous.handoffRate} lowerIsBetter />}
            help="Conversaciones del periodo que necesitaron a una persona: hoy esperan o están atendidas por tu equipo, o recibieron algún mensaje escrito por una persona. Es una aproximación: no se guarda el historial de estados."
          />
          <Stat
            label="1.ª respuesta"
            value={formatDuration(bot.averageFirstResponseSeconds)}
            delta={
              bot.averageFirstResponseSeconds != null && previous.averageFirstResponseSeconds != null ? (
                <DeltaBadge
                  current={bot.averageFirstResponseSeconds}
                  previous={previous.averageFirstResponseSeconds}
                  lowerIsBetter
                />
              ) : undefined
            }
            help="Tiempo medio desde el primer mensaje del cliente hasta la primera respuesta (del bot o de tu equipo)."
          />
        </dl>

        <div className="relative">
          <ChartContainer
            config={chartConfig}
            className="aspect-auto h-56 w-full"
            role="img"
            aria-label={summary}
          >
            <BarChart data={daily} margin={{ top: 4, right: 4, left: 0, bottom: 0 }} barCategoryGap="20%">
              <CartesianGrid vertical={false} strokeDasharray="3 3" />
              <XAxis
                dataKey="date"
                tickLine={false}
                axisLine={false}
                tickMargin={8}
                minTickGap={20}
                tickFormatter={formatDayShort}
              />
              <YAxis
                width={36}
                tickLine={false}
                axisLine={false}
                allowDecimals={false}
                tickFormatter={(v: number) => formatNumber(v)}
              />
              <ChartTooltip
                cursor={{ fill: 'var(--muted)', opacity: 0.6 }}
                content={
                  <AnalyticsTooltip
                    formatLabel={(d) => capitalize(formatDayLong(d))}
                    names={{ botMessages: 'Bot', humanMessages: 'Equipo' }}
                    formatValue={(v) => `${formatNumber(v)} mensajes`}
                    showTotal
                  />
                }
              />
              <ChartLegend content={<ChartLegendContent />} />
              <Bar
                dataKey="botMessages"
                stackId="messages"
                fill="var(--color-botMessages)"
                stroke="var(--card)"
                strokeWidth={1}
                maxBarSize={32}
              />
              <Bar
                dataKey="humanMessages"
                stackId="messages"
                fill="var(--color-humanMessages)"
                stroke="var(--card)"
                strokeWidth={1}
                radius={[4, 4, 0, 0]}
                maxBarSize={32}
              />
            </BarChart>
          </ChartContainer>
          {totalMessages === 0 && (
            <ChartEmptyOverlay
              title="Sin mensajes enviados en este periodo"
              description="Prueba a tu vendedor desde «Mi vendedor → Probar vendedor»."
            />
          )}
        </div>
      </CardContent>
    </Card>
  )
}

function Stat({
  label,
  value,
  sub,
  delta,
  help,
}: {
  label: string
  value: string
  sub?: string
  delta?: ReactNode
  help: string
}) {
  return (
    <div className="bg-muted/40 min-w-0 space-y-1 rounded-lg p-3">
      <dt className="text-muted-foreground flex items-center gap-1 text-xs font-medium">
        <span className="truncate">{label}</span>
        <Tooltip>
          <TooltipTrigger asChild>
            <button
              type="button"
              className="text-muted-foreground hover:text-foreground rounded-sm"
              aria-label={`Qué es «${label}»`}
            >
              <Info className="size-3.5" aria-hidden />
            </button>
          </TooltipTrigger>
          <TooltipContent className="max-w-64">{help}</TooltipContent>
        </Tooltip>
      </dt>
      <dd className="font-display truncate text-xl font-semibold tabular-nums">{value}</dd>
      {sub && <dd className="text-muted-foreground truncate text-xs">{sub}</dd>}
      {delta && <dd>{delta}</dd>}
    </div>
  )
}
