import { Link } from '@tanstack/react-router'
import { Radio } from 'lucide-react'
import { Cell, Label, Pie, PieChart } from 'recharts'
import { ChannelIcon, channelMeta } from '@/components/shared/channel'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { ChartContainer, ChartTooltip, type ChartConfig } from '@/components/ui/chart'
import { formatNumber, formatPercent } from '@/lib/format'
import type { DashboardData } from '../../api'
import { channelColors } from '../../utils'
import { AnalyticsTooltip } from '../chart-tooltip'

const chartConfig = {
  WhatsApp: { label: 'WhatsApp', color: channelColors.WhatsApp },
  Instagram: { label: 'Instagram', color: channelColors.Instagram },
  FacebookMessenger: { label: 'Messenger', color: channelColors.FacebookMessenger },
} satisfies ChartConfig

/** Donut de conversaciones por canal (14 días) con leyenda y porcentajes. */
export function ChannelDonut({ channels }: { channels: DashboardData['channels'] }) {
  const total = channels.reduce((s, c) => s + c.conversations, 0)
  const data = channels
    .filter((c) => c.conversations > 0)
    .map((c) => ({ channel: c.channel, name: channelMeta[c.channel].label, value: c.conversations }))
  const summary =
    total === 0
      ? 'Sin conversaciones en los últimos 14 días.'
      : `Conversaciones por canal en los últimos 14 días: ` +
        data
          .map((d) => `${d.name} ${formatNumber(d.value)} (${formatPercent((d.value / total) * 100, 0)})`)
          .join(', ') +
        '.'

  return (
    <Card className="gap-4">
      <CardHeader>
        <CardTitle>Conversaciones por canal</CardTitle>
        <CardDescription>Últimos 14 días</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-1 flex-col gap-4">
        {total === 0 ? (
          <div className="flex flex-1 flex-col items-center justify-center gap-3 rounded-xl border border-dashed px-4 py-8 text-center">
            <div className="bg-primary/10 text-primary flex size-10 items-center justify-center rounded-full">
              <Radio className="size-5" aria-hidden />
            </div>
            <div className="space-y-1">
              <p className="text-sm font-medium">Sin conversaciones todavía</p>
              <p className="text-muted-foreground text-xs">
                Conecta tus canales para que tus clientes te escriban.
              </p>
            </div>
            <Button asChild size="sm" variant="outline">
              <Link to="/channels">Ver canales</Link>
            </Button>
          </div>
        ) : (
          <>
            <ChartContainer
              config={chartConfig}
              className="mx-auto aspect-square h-48"
              role="img"
              aria-label={summary}
            >
              <PieChart>
                <ChartTooltip
                  content={<AnalyticsTooltip formatValue={(v) => `${formatNumber(v)} conversaciones`} />}
                />
                <Pie
                  data={data}
                  dataKey="value"
                  nameKey="name"
                  innerRadius={58}
                  outerRadius={84}
                  paddingAngle={data.length > 1 ? 2 : 0}
                  cornerRadius={4}
                  stroke="var(--card)"
                  strokeWidth={2}
                >
                  {data.map((d) => (
                    <Cell key={d.channel} fill={`var(--color-${d.channel})`} />
                  ))}
                  <Label
                    content={({ viewBox }) => {
                      if (!viewBox || !('cx' in viewBox)) return null
                      const { cx, cy } = viewBox as { cx: number; cy: number }
                      return (
                        <text x={cx} y={cy} textAnchor="middle" dominantBaseline="middle">
                          <tspan
                            x={cx}
                            y={cy - 4}
                            className="fill-foreground font-display text-2xl font-semibold"
                          >
                            {formatNumber(total)}
                          </tspan>
                          <tspan x={cx} y={cy + 16} className="fill-muted-foreground text-xs">
                            conversaciones
                          </tspan>
                        </text>
                      )
                    }}
                  />
                </Pie>
              </PieChart>
            </ChartContainer>
            <ul className="space-y-2" aria-label="Detalle por canal">
              {channels.map((c) => {
                const share = total ? (c.conversations / total) * 100 : 0
                return (
                  <li key={c.channel} className="flex items-center gap-2.5 text-sm">
                    <ChannelIcon channel={c.channel} />
                    <span className="flex-1">{channelMeta[c.channel].label}</span>
                    <span className="font-medium tabular-nums">{formatNumber(c.conversations)}</span>
                    <span className="text-muted-foreground w-12 text-right text-xs tabular-nums">
                      {formatPercent(share, 0)}
                    </span>
                  </li>
                )
              })}
            </ul>
          </>
        )}
      </CardContent>
    </Card>
  )
}
