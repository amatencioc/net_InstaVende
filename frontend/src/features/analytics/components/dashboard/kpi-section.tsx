import { Link } from '@tanstack/react-router'
import type { LucideIcon } from 'lucide-react'
import {
  ChevronRight,
  CircleCheck,
  Hand,
  MessagesSquare,
  Receipt,
  ShoppingBag,
  TrendingUp,
  ClipboardCheck,
} from 'lucide-react'
import { useState } from 'react'
import { StatCard } from '@/components/shared/stat-card'
import { Skeleton } from '@/components/ui/skeleton'
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group'
import { useMoney } from '@/lib/auth/session'
import { formatNumber } from '@/lib/format'
import { cn } from '@/lib/utils'
import type { FileRoutesByTo } from '@/routeTree.gen'
import type { DashboardData } from '../../api'
import { percentChange } from '../../utils'

type KpiRange = 'today' | 'week'

/** Fila de KPIs con selector Hoy / 7 días y comparativa con el periodo anterior equivalente. */
export function KpiSection({ data }: { data?: DashboardData }) {
  const money = useMoney()
  const [range, setRange] = useState<KpiRange>('today')
  const current = data ? (range === 'today' ? data.today : data.last7Days) : undefined
  const previous = data ? (range === 'today' ? data.yesterday : data.previous7Days) : undefined
  const loading = !current || !previous

  const delta = (pick: (k: NonNullable<typeof current>) => number) =>
    current && previous ? percentChange(pick(current), pick(previous)) : null
  const noBase = (pick: (k: NonNullable<typeof current>) => number) =>
    current && previous && delta(pick) === null ? 'sin datos previos' : undefined

  return (
    <section aria-labelledby="kpi-title" className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 id="kpi-title" className="text-base font-semibold">
            Resumen
          </h2>
          <p className="text-muted-foreground text-xs">
            {range === 'today'
              ? 'Hoy hasta ahora, comparado con ayer a la misma hora.'
              : 'Últimos 7 días, comparado con los 7 días anteriores.'}
          </p>
        </div>
        <ToggleGroup
          type="single"
          variant="outline"
          size="sm"
          value={range}
          onValueChange={(v) => v && setRange(v as KpiRange)}
          aria-label="Periodo del resumen"
        >
          <ToggleGroupItem value="today" className="px-3">
            Hoy
          </ToggleGroupItem>
          <ToggleGroupItem value="week" className="px-3">
            7 días
          </ToggleGroupItem>
        </ToggleGroup>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard
          label="Ingresos"
          icon={TrendingUp}
          tone="primary"
          loading={loading}
          value={money(current?.revenue)}
          delta={delta((k) => k.revenue)}
          hint={noBase((k) => k.revenue)}
        />
        <StatCard
          label="Pedidos"
          icon={ShoppingBag}
          tone="accent"
          loading={loading}
          value={formatNumber(current?.orders)}
          delta={delta((k) => k.orders)}
          hint={noBase((k) => k.orders)}
        />
        <StatCard
          label="Ticket promedio"
          icon={Receipt}
          tone="info"
          loading={loading}
          value={money(current?.averageTicket)}
          delta={delta((k) => k.averageTicket)}
          hint={noBase((k) => k.averageTicket)}
        />
        <StatCard
          label="Conversaciones nuevas"
          icon={MessagesSquare}
          tone="warning"
          loading={loading}
          value={formatNumber(current?.newConversations)}
          delta={delta((k) => k.newConversations)}
          hint={current ? `${formatNumber(current.inboundMessages)} mensajes recibidos` : undefined}
        />
      </div>

      <div className="grid gap-3 sm:grid-cols-2">
        {data ? (
          <>
            <ActionTile
              to="/inbox"
              icon={Hand}
              count={data.conversationsWaitingHuman}
              label={
                data.conversationsWaitingHuman === 1
                  ? 'conversación espera a una persona'
                  : 'conversaciones esperan a una persona'
              }
              doneLabel="Nadie espera atención humana"
              cta="Atender"
            />
            <ActionTile
              to="/orders"
              icon={ClipboardCheck}
              count={data.ordersInValidation}
              label={data.ordersInValidation === 1 ? 'pedido por validar' : 'pedidos por validar'}
              doneLabel="No hay pedidos por validar"
              cta="Revisar"
            />
          </>
        ) : (
          <>
            <Skeleton className="h-16 rounded-xl" />
            <Skeleton className="h-16 rounded-xl" />
          </>
        )}
      </div>
    </section>
  )
}

interface ActionTileProps {
  to: keyof FileRoutesByTo
  icon: LucideIcon
  count: number
  label: string
  doneLabel: string
  cta: string
}

/** Indicador operativo enlazado: destaca en ámbar cuando hay trabajo pendiente. */
function ActionTile({ to, icon: Icon, count, label, doneLabel, cta }: ActionTileProps) {
  const pending = count > 0
  return (
    <Link
      to={to}
      className={cn(
        'group flex items-center gap-3 rounded-xl border px-4 py-3 transition-colors outline-none',
        'focus-visible:ring-ring/50 focus-visible:ring-[3px]',
        pending ? 'border-warning/40 bg-warning/8 hover:bg-warning/12' : 'bg-card hover:bg-muted/50',
      )}
    >
      <span
        className={cn(
          'flex size-9 shrink-0 items-center justify-center rounded-lg',
          pending ? 'bg-warning/20 text-warning' : 'bg-success/10 text-success',
        )}
      >
        {pending ? (
          <Icon className="size-4.5" aria-hidden />
        ) : (
          <CircleCheck className="size-4.5" aria-hidden />
        )}
      </span>
      <span className="min-w-0 flex-1 text-sm">
        {pending ? (
          <>
            <span className="font-display text-lg font-semibold tabular-nums">{formatNumber(count)}</span>{' '}
            <span className="text-muted-foreground">{label}</span>
          </>
        ) : (
          <span className="text-muted-foreground">{doneLabel}</span>
        )}
      </span>
      {pending && (
        <span className="text-foreground inline-flex items-center gap-0.5 text-sm font-medium">
          {cta}
          <ChevronRight className="size-4 transition-transform group-hover:translate-x-0.5" aria-hidden />
        </span>
      )}
    </Link>
  )
}
