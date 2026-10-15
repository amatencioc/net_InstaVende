import { keepPreviousData, queryOptions, useMutation, useQueryClient } from '@tanstack/react-query'
import { format } from 'date-fns'
import { toast } from 'sonner'
import { api, unwrap, type Schemas } from '@/lib/api/client'

export type OrderBoardTab = Schemas['OrderBoardTab']
export type OrderSubStatus = Schemas['OrderSubStatus']
export type OrderWorkflow = Schemas['OrderWorkflowDto']
export type OrderSubStatusInfo = Schemas['OrderSubStatusInfoDto']
export type OrderBoard = Schemas['OrderBoardDto']
export type OrderBoardColumn = Schemas['OrderBoardColumnDto']
export type OrderCard = Schemas['OrderCardDto']
export type OrderDetail = Schemas['OrderDetailDto']
export type OrderUpdateRequest = Schemas['OrderUpdateRequest']

export const orderTabs = ['Nuevos', 'EnCurso', 'Completados', 'Cancelados'] as const satisfies readonly OrderBoardTab[]

/** Filtros del tablero (fechas locales del negocio en formato yyyy-MM-dd). */
export interface OrderFilters {
  search?: string
  from?: string
  to?: string
}

export const orderKeys = {
  all: ['orders'] as const,
  workflow: () => [...orderKeys.all, 'workflow'] as const,
  boards: () => [...orderKeys.all, 'board'] as const,
  board: (tab: OrderBoardTab, filters: OrderFilters) => [...orderKeys.boards(), { tab, ...filters }] as const,
  details: () => [...orderKeys.all, 'detail'] as const,
  detail: (id: number) => [...orderKeys.details(), id] as const,
}

function queryFilters(filters: OrderFilters) {
  return {
    search: filters.search || undefined,
    from: filters.from || undefined,
    to: filters.to || undefined,
  }
}

/** Pestañas, columnas y etiquetas del flujo (no cambian durante la sesión). */
export const orderWorkflowQuery = queryOptions({
  queryKey: orderKeys.workflow(),
  queryFn: () => unwrap(api.GET('/api/orders/workflow')),
  staleTime: Infinity,
  gcTime: Infinity,
})

export const orderBoardQuery = (tab: OrderBoardTab, filters: OrderFilters) =>
  queryOptions({
    queryKey: orderKeys.board(tab, filters),
    queryFn: ({ signal }) =>
      unwrap(api.GET('/api/orders/board', { params: { query: { tab, ...queryFilters(filters) } }, signal })),
    placeholderData: keepPreviousData,
    // Los pedidos los crea el bot en segundo plano: refresco periódico ligero
    refetchInterval: 60_000,
  })

export const orderDetailQuery = (id: number) =>
  queryOptions({
    queryKey: orderKeys.detail(id),
    queryFn: ({ signal }) => unwrap(api.GET('/api/orders/{id}', { params: { path: { id } }, signal })),
  })

// ── Mover de estado (optimista) ───────────────────────────────────────────────

export interface MoveOrderVars {
  id: number
  orderNumber: string
  target: OrderSubStatusInfo
  notifyCustomer: boolean
}

/** Aplica el movimiento a un tablero en caché (quita la tarjeta y la inserta en su nueva columna si está visible). */
function applyMoveToBoard(board: OrderBoard, vars: MoveOrderVars): OrderBoard {
  let card: OrderCard | undefined
  for (const col of board.columns) {
    card = col.orders.find((o) => o.id === vars.id)
    if (card) break
  }
  if (!card || card.subStatus === vars.target.subStatus) return board
  const moved: OrderCard = {
    ...card,
    subStatus: vars.target.subStatus,
    subStatusLabel: vars.target.label,
    subStatusCss: vars.target.css,
  }
  const sourceSub = card.subStatus
  const sourceTab = board.columns.some((c) => c.subStatus === sourceSub) ? board.tab : undefined

  const columns = board.columns.map((col) => {
    if (col.subStatus === sourceSub) {
      return {
        ...col,
        count: Math.max(0, col.count - 1),
        totalAmount: col.totalAmount - moved.total,
        orders: col.orders.filter((o) => o.id !== vars.id),
      }
    }
    if (col.subStatus === vars.target.subStatus) {
      const orders = [...col.orders, moved].sort(
        (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime() || b.id - a.id,
      )
      return { ...col, count: col.count + 1, totalAmount: col.totalAmount + moved.total, orders }
    }
    return col
  })

  const tabCounts = board.tabCounts.map((t) => {
    if (t.tab === vars.target.tab && t.tab === sourceTab) return t
    if (t.tab === sourceTab) return { ...t, count: Math.max(0, t.count - 1) }
    if (t.tab === vars.target.tab) return { ...t, count: t.count + 1 }
    return t
  })

  return { ...board, columns, tabCounts }
}

export function useMoveOrder() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, target, notifyCustomer }: MoveOrderVars) =>
      unwrap(
        api.POST('/api/orders/{id}/move', {
          params: { path: { id } },
          body: { subStatus: target.subStatus, notifyCustomer },
        }),
      ),
    onMutate: async (vars) => {
      await Promise.all([
        qc.cancelQueries({ queryKey: orderKeys.boards() }),
        qc.cancelQueries({ queryKey: orderKeys.detail(vars.id) }),
      ])
      const boards = qc.getQueriesData<OrderBoard>({ queryKey: orderKeys.boards() })
      const detail = qc.getQueryData<OrderDetail>(orderKeys.detail(vars.id))

      for (const [key, data] of boards) {
        if (data) qc.setQueryData<OrderBoard>(key, applyMoveToBoard(data, vars))
      }
      if (detail) {
        qc.setQueryData<OrderDetail>(orderKeys.detail(vars.id), {
          ...detail,
          subStatus: vars.target.subStatus,
          subStatusLabel: vars.target.label,
          subStatusCss: vars.target.css,
          tab: vars.target.tab,
          status: vars.target.status,
        })
      }
      return { boards, detail }
    },
    onError: (_error, vars, context) => {
      // Rollback: restaura exactamente lo que había antes del movimiento
      for (const [key, data] of context?.boards ?? []) qc.setQueryData(key, data)
      if (context?.detail) qc.setQueryData(orderKeys.detail(vars.id), context.detail)
    },
    onSuccess: (result, vars) => {
      toast.success(`Pedido ${vars.orderNumber} movido a «${result.subStatusLabel}».`)
      if (vars.notifyCustomer) {
        if (result.customerNotified) toast.success('Se avisó al cliente por su canal.')
        else if (result.notificationError) toast.warning(result.notificationError)
      }
    },
    onSettled: (_data, _error, vars) =>
      Promise.all([
        qc.invalidateQueries({ queryKey: orderKeys.boards() }),
        qc.invalidateQueries({ queryKey: orderKeys.detail(vars.id) }),
      ]),
  })
}

// ── Edición administrativa ───────────────────────────────────────────────────

export function useUpdateOrder(id: number) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: OrderUpdateRequest) =>
      unwrap(api.PATCH('/api/orders/{id}', { params: { path: { id } }, body })),
    meta: { successMessage: 'Datos del pedido guardados.' },
    onSuccess: (detail) => {
      qc.setQueryData(orderKeys.detail(id), detail)
      void qc.invalidateQueries({ queryKey: orderKeys.boards() })
    },
  })
}

// ── Exportación CSV ──────────────────────────────────────────────────────────

function fileNameFrom(disposition: string | null): string | null {
  if (!disposition) return null
  const star = /filename\*=UTF-8''([^;]+)/i.exec(disposition)
  if (star?.[1]) {
    try {
      return decodeURIComponent(star[1])
    } catch {
      /* cae al nombre simple */
    }
  }
  const plain = /filename="?([^";]+)"?/i.exec(disposition)
  return plain?.[1] ?? null
}

/** Descarga el CSV de pedidos (fetch con credenciales de la sesión) respetando los filtros. */
export function useExportOrders() {
  return useMutation({
    mutationFn: async ({ tab, filters }: { tab?: OrderBoardTab; filters: OrderFilters }) => {
      const request = api.GET('/api/orders/export', {
        params: { query: { tab, ...queryFilters(filters) } },
        parseAs: 'blob',
      })
      // unwrap lanza ApiError con el mensaje del servidor (ProblemDetails) si falla
      const blob = (await unwrap(request)) as Blob
      const { response } = await request
      const name =
        fileNameFrom(response.headers.get('Content-Disposition')) ?? `pedidos-${format(new Date(), 'yyyyMMdd')}.csv`
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = name
      document.body.appendChild(link)
      link.click()
      link.remove()
      setTimeout(() => URL.revokeObjectURL(url), 1_000)
      return name
    },
    meta: { successMessage: 'Exportación lista. Revisa tus descargas.' },
  })
}
