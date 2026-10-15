import { useQuery } from '@tanstack/react-query'
import { getRouteApi, Link } from '@tanstack/react-router'
import { BarChart3, Download, Loader2, Percent, Receipt, ShoppingBag, TrendingUp } from 'lucide-react'
import { EmptyState } from '@/components/shared/empty-state'
import { PageContainer, PageHeader } from '@/components/shared/page-header'
import { QueryError } from '@/components/shared/query-state'
import { StatCard } from '@/components/shared/stat-card'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { useMoney, useSession } from '@/lib/auth/session'
import { formatNumber, formatPercent } from '@/lib/format'
import { metricsQuery, type MetricsData } from '../api'
import { exportMetricsCsv } from '../export-metrics'
import { resolveMetricsParams, type MetricsParams } from '../schemas'
import { formatRange, percentChange } from '../utils'
import { BotCard } from './metrics/bot-card'
import { ChannelRevenueCard, SubStatusCard } from './metrics/breakdown-cards'
import { FunnelCard } from './metrics/funnel-card'
import { PeriodPicker } from './metrics/period-picker'
import { TopCustomersCard, TopProductsCard } from './metrics/ranking-cards'
import { SalesChart } from './metrics/sales-chart'

const routeApi = getRouteApi('/_app/metrics')

/** Página de métricas: ventas, embudo, bot y rankings con comparativa de periodos. */
export function MetricsPage() {
  const search = routeApi.useSearch()
  const navigate = routeApi.useNavigate()
  const params = resolveMetricsParams(search)
  const { data, error, isError, isFetching, isPlaceholderData, refetch } = useQuery(metricsQuery(params))
  const { business } = useSession()

  const changePeriod = (next: MetricsParams) =>
    void navigate({ search: { period: next.period, from: next.from, to: next.to }, replace: true })

  return (
    <PageContainer>
      <PageHeader
        title="Métricas"
        description={
          data ? (
            <span className="inline-flex flex-wrap items-center gap-x-1.5">
              <span className="text-foreground font-medium">
                {formatRange(data.range.from, data.range.to)}
              </span>
              <span>· comparado con {formatRange(data.range.previousFrom, data.range.previousTo)}</span>
              {isFetching && <Loader2 className="size-3.5 animate-spin" aria-label="Actualizando" />}
            </span>
          ) : (
            'Rendimiento de tus ventas y de tu vendedor IA.'
          )
        }
        actions={
          <>
            <PeriodPicker value={params} onChange={changePeriod} />
            <Button
              variant="outline"
              disabled={!data || isPlaceholderData}
              onClick={() => data && exportMetricsCsv(data, business.currencySymbol)}
            >
              <Download aria-hidden /> Exportar
            </Button>
          </>
        }
      />

      {isError && !data ? (
        <QueryError error={error} onRetry={() => void refetch()} />
      ) : (
        <div
          className={isPlaceholderData ? 'opacity-60 transition-opacity' : 'transition-opacity'}
          aria-busy={isFetching}
        >
          <MetricsContent data={data} />
        </div>
      )}
    </PageContainer>
  )
}

function MetricsContent({ data }: { data?: MetricsData }) {
  const money = useMoney()
  const sales = data?.sales
  const prev = data?.previousSales
  const delta = (pick: (s: MetricsData['sales']) => number) =>
    sales && prev ? percentChange(pick(sales), pick(prev)) : null
  const hint = (pick: (s: MetricsData['sales']) => number) =>
    sales && prev && delta(pick) === null ? 'sin datos previos' : 'vs periodo anterior'

  const isEmpty =
    !!data &&
    data.sales.conversations === 0 &&
    data.sales.orders === 0 &&
    data.bot.botMessages + data.bot.humanMessages === 0

  return (
    <div className="flex flex-col gap-6">
      {isEmpty && (
        <EmptyState
          icon={BarChart3}
          title="Todavía no hay actividad en este periodo"
          description="Las métricas se calculan con las conversaciones y pedidos de tus canales. Conecta un canal y prueba a tu vendedor para empezar a ver resultados."
          action={
            <div className="flex flex-wrap justify-center gap-2">
              <Button asChild size="sm">
                <Link to="/channels">Conectar canal</Link>
              </Button>
              <Button asChild size="sm" variant="outline">
                <Link to="/seller/test">Probar vendedor</Link>
              </Button>
            </div>
          }
        />
      )}

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard
          label="Ingresos"
          icon={TrendingUp}
          tone="primary"
          loading={!sales}
          value={money(sales?.revenue)}
          delta={delta((s) => s.revenue)}
          hint={hint((s) => s.revenue)}
        />
        <StatCard
          label="Pedidos"
          icon={ShoppingBag}
          tone="accent"
          loading={!sales}
          value={formatNumber(sales?.orders)}
          delta={delta((s) => s.orders)}
          hint={hint((s) => s.orders)}
        />
        <StatCard
          label="Ticket promedio"
          icon={Receipt}
          tone="info"
          loading={!sales}
          value={money(sales?.averageTicket)}
          delta={delta((s) => s.averageTicket)}
          hint={hint((s) => s.averageTicket)}
        />
        <StatCard
          label="Tasa de conversión"
          icon={Percent}
          tone="warning"
          loading={!sales}
          value={formatPercent(sales?.conversionRate)}
          delta={delta((s) => s.conversionRate)}
          hint={
            sales
              ? `${formatNumber(sales.conversationsWithOrder)} de ${formatNumber(sales.conversations)} chats`
              : undefined
          }
        />
      </div>

      {data ? (
        <>
          <SalesChart daily={data.daily} />
          <div className="grid gap-4 lg:grid-cols-2">
            <FunnelCard funnel={data.funnel} />
            <BotCard bot={data.bot} previous={data.previousBot} daily={data.daily} />
          </div>
          <div className="grid gap-4 lg:grid-cols-2">
            <ChannelRevenueCard channels={data.channels} />
            <SubStatusCard subStatuses={data.subStatuses} />
          </div>
          <div className="grid gap-4 lg:grid-cols-2">
            <TopProductsCard byUnits={data.topProductsByUnits} byRevenue={data.topProductsByRevenue} />
            <TopCustomersCard customers={data.topCustomers} />
          </div>
        </>
      ) : (
        <>
          <Skeleton className="h-96 rounded-xl" />
          <div className="grid gap-4 lg:grid-cols-2">
            <Skeleton className="h-96 rounded-xl" />
            <Skeleton className="h-96 rounded-xl" />
          </div>
          <div className="grid gap-4 lg:grid-cols-2">
            <Skeleton className="h-72 rounded-xl" />
            <Skeleton className="h-72 rounded-xl" />
          </div>
        </>
      )}
    </div>
  )
}
