import { useId, useMemo, useState } from 'react'
import { Area, AreaChart, CartesianGrid, XAxis, YAxis } from 'recharts'
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { ChartContainer, ChartTooltip, type ChartConfig } from '@/components/ui/chart'
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group'
import { useMoney } from '@/lib/auth/session'
import { formatNumber } from '@/lib/format'
import type { DashboardData } from '../../api'
import { formatDayLong, formatDayShort, useCompactMoney, capitalize } from '../../utils'
import { AnalyticsTooltip, ChartEmptyOverlay } from '../chart-tooltip'

type Metric = 'revenue' | 'orders' | 'conversations'

const chartConfig = {
  revenue: { label: 'Ingresos', color: 'var(--chart-1)' },
  orders: { label: 'Pedidos', color: 'var(--chart-2)' },
  conversations: { label: 'Conversaciones', color: 'var(--chart-3)' },
} satisfies ChartConfig

/** Área de los últimos 14 días con selector de métrica (una sola escala, sin doble eje). */
export function ActivityChart({ daily }: { daily: DashboardData['daily'] }) {
  const [metric, setMetric] = useState<Metric>('revenue')
  const money = useMoney()
  const compactMoney = useCompactMoney()
  const gradientId = `activity-${useId().replace(/:/g, '')}`
  const label = chartConfig[metric].label
  const format = (v: number) => (metric === 'revenue' ? money(v) : formatNumber(v))

  const { total, peak } = useMemo(() => {
    let sum = 0
    let best: { date: string; value: number } | null = null
    for (const d of daily) {
      const v = d[metric]
      sum += v
      if (v > 0 && (!best || v > best.value)) best = { date: d.date, value: v }
    }
    return { total: sum, peak: best }
  }, [daily, metric])

  const summary =
    total === 0
      ? `${label}: sin actividad en los últimos 14 días.`
      : `${label} de los últimos 14 días: total ${format(total)}.` +
        (peak
          ? ` El día con más actividad fue el ${formatDayLong(peak.date)} con ${format(peak.value)}.`
          : '')

  return (
    <Card className="gap-4">
      <CardHeader>
        <CardTitle>Actividad de los últimos 14 días</CardTitle>
        <CardDescription>
          Total {label.toLowerCase()}:{' '}
          <span className="text-foreground font-semibold tabular-nums">{format(total)}</span>
        </CardDescription>
        <CardAction>
          <ToggleGroup
            type="single"
            variant="outline"
            size="sm"
            value={metric}
            onValueChange={(v) => v && setMetric(v as Metric)}
            aria-label="Métrica del gráfico"
          >
            <ToggleGroupItem value="revenue" className="px-2.5">
              Ingresos
            </ToggleGroupItem>
            <ToggleGroupItem value="orders" className="px-2.5">
              Pedidos
            </ToggleGroupItem>
            <ToggleGroupItem value="conversations" className="px-2.5">
              Chats
            </ToggleGroupItem>
          </ToggleGroup>
        </CardAction>
      </CardHeader>
      <CardContent className="relative px-2 sm:px-6">
        <ChartContainer
          config={chartConfig}
          className="aspect-auto h-64 w-full"
          role="img"
          aria-label={summary}
        >
          <AreaChart data={daily} margin={{ top: 8, right: 8, left: 0, bottom: 0 }}>
            <defs>
              <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stopColor={`var(--color-${metric})`} stopOpacity={0.28} />
                <stop offset="100%" stopColor={`var(--color-${metric})`} stopOpacity={0.02} />
              </linearGradient>
            </defs>
            <CartesianGrid vertical={false} strokeDasharray="3 3" />
            <XAxis
              dataKey="date"
              tickLine={false}
              axisLine={false}
              tickMargin={8}
              minTickGap={24}
              tickFormatter={formatDayShort}
            />
            <YAxis
              width={metric === 'revenue' ? 64 : 36}
              tickLine={false}
              axisLine={false}
              allowDecimals={false}
              tickFormatter={(v: number) => (metric === 'revenue' ? compactMoney(v) : formatNumber(v))}
            />
            <ChartTooltip
              cursor={{ strokeDasharray: '4 4' }}
              content={
                <AnalyticsTooltip
                  formatLabel={(d) => capitalize(formatDayLong(d))}
                  names={{ [metric]: label }}
                  formatValue={(v) => format(v)}
                />
              }
            />
            <Area
              key={metric}
              dataKey={metric}
              type="monotone"
              stroke={`var(--color-${metric})`}
              strokeWidth={2}
              fill={`url(#${gradientId})`}
              activeDot={{ r: 4, strokeWidth: 2, stroke: 'var(--card)' }}
            />
          </AreaChart>
        </ChartContainer>
        {total === 0 && (
          <ChartEmptyOverlay
            title={`Aún no hay ${label.toLowerCase()} en estas dos semanas`}
            description="Cuando tu vendedor empiece a conversar y vender verás aquí la evolución diaria."
          />
        )}
      </CardContent>
    </Card>
  )
}
