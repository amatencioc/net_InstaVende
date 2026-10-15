import { useId, useMemo, useState } from 'react'
import { Area, AreaChart, Bar, BarChart, CartesianGrid, XAxis, YAxis } from 'recharts'
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { ChartContainer, ChartTooltip, type ChartConfig } from '@/components/ui/chart'
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group'
import { useMoney } from '@/lib/auth/session'
import { formatNumber } from '@/lib/format'
import type { MetricsData } from '../../api'
import { capitalize, formatDayLong, formatDayShort, useCompactMoney } from '../../utils'
import { AnalyticsTooltip, ChartEmptyOverlay } from '../chart-tooltip'

type Metric = 'revenue' | 'orders'

const chartConfig = {
  revenue: { label: 'Ingresos', color: 'var(--chart-1)' },
  orders: { label: 'Pedidos', color: 'var(--chart-2)' },
} satisfies ChartConfig

/**
 * Evolución diaria de ingresos o pedidos (una métrica a la vez: nunca doble eje).
 * Barras hasta 31 días; área para periodos más largos.
 */
export function SalesChart({ daily }: { daily: MetricsData['daily'] }) {
  const [metric, setMetric] = useState<Metric>('revenue')
  const money = useMoney()
  const compactMoney = useCompactMoney()
  const gradientId = `sales-${useId().replace(/:/g, '')}`
  const label = chartConfig[metric].label
  const format = (v: number) => (metric === 'revenue' ? money(v) : formatNumber(v))
  const useBars = daily.length <= 31

  const stats = useMemo(() => {
    const values = daily.map((d) => d[metric])
    const total = values.reduce((a, b) => a + b, 0)
    let peakIndex = -1
    values.forEach((v, i) => {
      if (v > 0 && (peakIndex < 0 || v > (values[peakIndex] ?? 0))) peakIndex = i
    })
    return {
      total,
      average: daily.length ? total / daily.length : 0,
      peak: peakIndex >= 0 ? daily[peakIndex] : undefined,
    }
  }, [daily, metric])

  const summary =
    stats.total === 0
      ? `${label}: sin datos en el periodo.`
      : `${label} por día. Total ${format(stats.total)}, promedio diario ${format(stats.average)}.` +
        (stats.peak ? ` Mejor día: ${formatDayLong(stats.peak.date)} con ${format(stats.peak[metric])}.` : '')

  const axisProps = {
    x: {
      dataKey: 'date',
      tickLine: false,
      axisLine: false,
      tickMargin: 8,
      minTickGap: 20,
      tickFormatter: formatDayShort,
    },
    y: {
      width: metric === 'revenue' ? 64 : 36,
      tickLine: false,
      axisLine: false,
      allowDecimals: false,
      tickFormatter: (v: number) => (metric === 'revenue' ? compactMoney(v) : formatNumber(v)),
    },
  }
  const tooltip = (
    <ChartTooltip
      cursor={useBars ? { fill: 'var(--muted)', opacity: 0.6 } : { strokeDasharray: '4 4' }}
      content={
        <AnalyticsTooltip
          formatLabel={(d) => capitalize(formatDayLong(d))}
          names={{ [metric]: label }}
          formatValue={(v) => format(v)}
        />
      }
    />
  )

  return (
    <Card className="gap-4">
      <CardHeader>
        <CardTitle>{metric === 'revenue' ? 'Ingresos por día' : 'Pedidos por día'}</CardTitle>
        <CardDescription>
          Total <span className="text-foreground font-semibold tabular-nums">{format(stats.total)}</span> ·
          promedio diario{' '}
          <span className="text-foreground font-medium tabular-nums">{format(stats.average)}</span>
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
          </ToggleGroup>
        </CardAction>
      </CardHeader>
      <CardContent className="relative px-2 sm:px-6">
        <ChartContainer
          config={chartConfig}
          className="aspect-auto h-72 w-full"
          role="img"
          aria-label={summary}
        >
          {useBars ? (
            <BarChart data={daily} margin={{ top: 8, right: 8, left: 0, bottom: 0 }} barCategoryGap="20%">
              <CartesianGrid vertical={false} strokeDasharray="3 3" />
              <XAxis {...axisProps.x} />
              <YAxis {...axisProps.y} />
              {tooltip}
              <Bar
                key={metric}
                dataKey={metric}
                fill={`var(--color-${metric})`}
                radius={[4, 4, 0, 0]}
                maxBarSize={40}
              />
            </BarChart>
          ) : (
            <AreaChart data={daily} margin={{ top: 8, right: 8, left: 0, bottom: 0 }}>
              <defs>
                <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor={`var(--color-${metric})`} stopOpacity={0.28} />
                  <stop offset="100%" stopColor={`var(--color-${metric})`} stopOpacity={0.02} />
                </linearGradient>
              </defs>
              <CartesianGrid vertical={false} strokeDasharray="3 3" />
              <XAxis {...axisProps.x} />
              <YAxis {...axisProps.y} />
              {tooltip}
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
          )}
        </ChartContainer>
        {stats.total === 0 && (
          <ChartEmptyOverlay
            title={`Sin ${label.toLowerCase()} en este periodo`}
            description="Prueba con un periodo más amplio o revisa que tu vendedor esté conectado."
          />
        )}
      </CardContent>
    </Card>
  )
}
