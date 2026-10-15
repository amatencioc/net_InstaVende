import { ChannelIcon, channelMeta } from '@/components/shared/channel'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { useMoney } from '@/lib/auth/session'
import { formatNumber, formatPercent } from '@/lib/format'
import { cn } from '@/lib/utils'
import type { MetricsData } from '../../api'
import { statusTone } from '@/components/shared/order-status'
import { channelColors } from '../../utils'

/** Ingresos por canal: barras horizontales con el color de marca de cada canal. */
export function ChannelRevenueCard({ channels }: { channels: MetricsData['channels'] }) {
  const money = useMoney()
  const totalRevenue = channels.reduce((s, c) => s + c.revenue, 0)
  const max = Math.max(1, ...channels.map((c) => c.revenue))

  return (
    <Card className="gap-4">
      <CardHeader>
        <CardTitle>Ingresos por canal</CardTitle>
        <CardDescription>Dónde se concretan tus ventas</CardDescription>
      </CardHeader>
      <CardContent>
        {channels.length === 0 ? (
          <p className="text-muted-foreground rounded-xl border border-dashed px-4 py-10 text-center text-sm">
            Sin actividad por canal en este periodo.
          </p>
        ) : (
          <ul className="space-y-4">
            {channels.map((c) => {
              const share = totalRevenue > 0 ? (c.revenue / totalRevenue) * 100 : 0
              const conversion = c.conversations > 0 ? (c.orders / c.conversations) * 100 : null
              return (
                <li key={c.channel} className="space-y-1.5">
                  <div className="flex items-center gap-2.5 text-sm">
                    <ChannelIcon channel={c.channel} />
                    <span className="flex-1 font-medium">{channelMeta[c.channel].label}</span>
                    <span className="font-semibold tabular-nums">{money(c.revenue)}</span>
                    <span className="text-muted-foreground w-12 text-right text-xs tabular-nums">
                      {formatPercent(share, 0)}
                    </span>
                  </div>
                  <div className="bg-muted h-2 overflow-hidden rounded-full" aria-hidden>
                    <div
                      className="h-full rounded-full transition-[width] duration-500"
                      style={{
                        width: `${Math.max(c.revenue > 0 ? 2 : 0, (c.revenue / max) * 100)}%`,
                        background: channelColors[c.channel],
                      }}
                    />
                  </div>
                  <p className="text-muted-foreground text-xs">
                    {formatNumber(c.orders)} {c.orders === 1 ? 'pedido' : 'pedidos'} ·{' '}
                    {formatNumber(c.conversations)}{' '}
                    {c.conversations === 1 ? 'conversación' : 'conversaciones'}
                    {conversion !== null && ` · ${formatPercent(conversion)} pedidos por conversación`}
                  </p>
                </li>
              )
            })}
          </ul>
        )}
      </CardContent>
    </Card>
  )
}

/** Distribución de los pedidos del periodo por sub-estado (columna del tablero). */
export function SubStatusCard({ subStatuses }: { subStatuses: MetricsData['subStatuses'] }) {
  const money = useMoney()
  const total = subStatuses.reduce((s, x) => s + x.orders, 0)
  const rows = subStatuses.filter((s) => s.orders > 0)

  return (
    <Card className="gap-4">
      <CardHeader>
        <CardTitle>Pedidos por estado</CardTitle>
        <CardDescription>
          {total > 0
            ? `${formatNumber(total)} pedidos creados en el periodo`
            : 'Estado actual de los pedidos del periodo'}
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {total === 0 ? (
          <p className="text-muted-foreground rounded-xl border border-dashed px-4 py-10 text-center text-sm">
            No hay pedidos en este periodo.
          </p>
        ) : (
          <>
            <div className="bg-muted flex h-3 gap-0.5 overflow-hidden rounded-full" aria-hidden>
              {rows.map((s) => (
                <div
                  key={s.subStatus}
                  className={cn('h-full first:rounded-l-full last:rounded-r-full', statusTone(s.css).dot)}
                  style={{ width: `${(s.orders / total) * 100}%` }}
                />
              ))}
            </div>
            <ul className="divide-y">
              {rows.map((s) => (
                <li key={s.subStatus} className="flex items-center gap-2.5 py-2 text-sm">
                  <span
                    className={cn('size-2.5 shrink-0 rounded-[3px]', statusTone(s.css).dot)}
                    aria-hidden
                  />
                  <span className="flex-1">{s.label}</span>
                  <span className="text-muted-foreground text-xs tabular-nums">{money(s.total)}</span>
                  <span className="w-10 text-right font-medium tabular-nums">{formatNumber(s.orders)}</span>
                  <span className="text-muted-foreground w-12 text-right text-xs tabular-nums">
                    {formatPercent((s.orders / total) * 100, 0)}
                  </span>
                </li>
              ))}
            </ul>
          </>
        )}
      </CardContent>
    </Card>
  )
}
