import { useQuery } from '@tanstack/react-query'
import { createFileRoute, Link } from '@tanstack/react-router'
import { FlaskConical, SearchX, ShoppingBag } from 'lucide-react'
import { useCallback, useMemo } from 'react'
import { z } from 'zod'
import { EmptyState } from '@/components/shared/empty-state'
import { PageContainer, PageHeader } from '@/components/shared/page-header'
import { QueryError } from '@/components/shared/query-state'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import {
  orderBoardQuery,
  orderTabs,
  orderWorkflowQuery,
  useExportOrders,
  type OrderBoardTab,
  type OrderCard,
  type OrderFilters,
  type OrderSubStatus,
} from '@/features/orders/api'
import { OrderBoard, OrderBoardSkeleton } from '@/features/orders/components/order-board'
import { OrderFiltersBar } from '@/features/orders/components/order-filters'
import { useOrderMover } from '@/features/orders/components/use-order-mover'
import { useNotifyPreference } from '@/features/orders/hooks'
import { cn } from '@/lib/utils'

const isoDate = /^\d{4}-\d{2}-\d{2}$/

const searchSchema = z.object({
  tab: z.enum(orderTabs).optional().catch(undefined),
  q: z.string().max(100).optional().catch(undefined),
  from: z.string().regex(isoDate).optional().catch(undefined),
  to: z.string().regex(isoDate).optional().catch(undefined),
})

export const Route = createFileRoute('/_app/orders/')({
  validateSearch: searchSchema,
  head: () => ({ meta: [{ title: 'Pedidos — InstaVende' }] }),
  component: OrdersPage,
})

function OrdersPage() {
  const search = Route.useSearch()
  const navigate = Route.useNavigate()
  const tab: OrderBoardTab = search.tab ?? 'Nuevos'
  const filters = useMemo<OrderFilters>(
    () => ({ search: search.q, from: search.from, to: search.to }),
    [search.q, search.from, search.to],
  )
  const hasFilters = Boolean(filters.search || filters.from || filters.to)

  const [notify, setNotify] = useNotifyPreference()
  const workflow = useQuery(orderWorkflowQuery)
  const board = useQuery(orderBoardQuery(tab, filters))
  const exporter = useExportOrders()
  const { move } = useOrderMover({ notifyPreference: notify })

  const onMove = useCallback((order: OrderCard, target: OrderSubStatus) => void move(order, target), [move])

  const setFilters = useCallback(
    (next: Partial<OrderFilters>) =>
      void navigate({
        search: (prev) => ({
          ...prev,
          q: 'search' in next ? next.search : prev.q,
          from: 'from' in next ? next.from : prev.from,
          to: 'to' in next ? next.to : prev.to,
        }),
        replace: true,
      }),
    [navigate],
  )

  const setTab = (value: string) =>
    void navigate({ search: (prev) => ({ ...prev, tab: value === 'Nuevos' ? undefined : (value as OrderBoardTab) }) })

  // Con keepPreviousData, al cambiar de pestaña no se muestran las columnas de la anterior
  const data = board.data && board.data.tab === tab ? board.data : undefined
  const counts = board.data?.tabCounts
  const tabsMeta = workflow.data?.tabs
  const activeLabel = tabsMeta?.find((t) => t.tab === tab)?.label ?? ''
  const columnsInTab = tabsMeta?.find((t) => t.tab === tab)?.columns.length ?? 3
  const totalOrders = counts?.reduce((sum, t) => sum + t.count, 0) ?? 0

  return (
    <PageContainer className="max-w-[96rem]">
      <PageHeader
        title="Pedidos"
        description="Gestiona los pedidos que registra tu vendedor IA en todos tus canales."
      />

      <OrderFiltersBar
        filters={filters}
        onChange={setFilters}
        notify={notify}
        onNotifyChange={setNotify}
        exporting={exporter.isPending}
        tabLabel={activeLabel}
        onExport={(scope) => exporter.mutate({ tab: scope === 'tab' ? tab : undefined, filters })}
      />

      {board.isError && !board.data ? (
        <QueryError error={board.error} onRetry={() => void board.refetch()} />
      ) : (
        <Tabs value={tab} onValueChange={setTab}>
          <div className="-mx-4 overflow-x-auto px-4 sm:mx-0 sm:px-0">
            {tabsMeta ? (
              <TabsList aria-label="Estados de los pedidos" className="h-10">
                {tabsMeta.map((t) => {
                  const count = counts?.find((c) => c.tab === t.tab)?.count
                  return (
                    <TabsTrigger key={t.tab} value={t.tab} className="gap-2 px-3">
                      {t.label}
                      <span
                        className={cn(
                          'min-w-6 rounded-full px-1.5 text-xs tabular-nums',
                          t.tab === tab ? 'bg-primary text-primary-foreground' : 'bg-muted-foreground/15',
                        )}
                      >
                        {count ?? '–'}
                        <span className="sr-only"> pedidos</span>
                      </span>
                    </TabsTrigger>
                  )
                })}
              </TabsList>
            ) : (
              <Skeleton className="h-10 w-full max-w-lg" />
            )}
          </div>

          {orderTabs.map((t) => (
            <TabsContent key={t} value={t} className="mt-4" aria-busy={board.isFetching}>
              {t !== tab ? null : !data ? (
                <OrderBoardSkeleton columns={columnsInTab} />
              ) : totalOrders === 0 && !hasFilters ? (
                <EmptyState
                  icon={ShoppingBag}
                  title="Aún no tienes pedidos"
                  description="Cuando tu vendedor IA cierre una venta por WhatsApp, Instagram o Messenger, el pedido aparecerá aquí para que lo gestiones."
                  action={
                    <Button asChild variant="outline">
                      <Link to="/seller/test">
                        <FlaskConical /> Probar mi vendedor
                      </Link>
                    </Button>
                  }
                />
              ) : totalOrders === 0 ? (
                <EmptyState
                  icon={SearchX}
                  title="No hay pedidos que coincidan"
                  description="Prueba con otro término de búsqueda o amplía el rango de fechas."
                  action={
                    <Button variant="outline" onClick={() => setFilters({ search: undefined, from: undefined, to: undefined })}>
                      Limpiar filtros
                    </Button>
                  }
                />
              ) : (
                <OrderBoard columns={data.columns} workflow={workflow.data} onMove={onMove} />
              )}
            </TabsContent>
          ))}
        </Tabs>
      )}
    </PageContainer>
  )
}
