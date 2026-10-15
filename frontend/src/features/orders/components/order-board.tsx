import {
  DndContext,
  DragOverlay,
  PointerSensor,
  TouchSensor,
  useDroppable,
  useSensor,
  useSensors,
  type Announcements,
  type DragEndEvent,
  type DragStartEvent,
} from '@dnd-kit/core'
import { Inbox } from 'lucide-react'
import { useCallback, useState, type ReactNode } from 'react'
import { Skeleton } from '@/components/ui/skeleton'
import { useMoney } from '@/lib/auth/session'
import { cn } from '@/lib/utils'
import type { OrderBoardColumn, OrderCard, OrderSubStatus, OrderWorkflow } from '../api'
import { statusTone } from '@/components/shared/order-status'
import { DraggableOrderCard, OrderCardView } from './order-card'
import { StatusDot } from '@/components/shared/order-status'

interface OrderBoardProps {
  columns: OrderBoardColumn[]
  workflow: OrderWorkflow | undefined
  onMove: (order: OrderCard, target: OrderSubStatus) => void
}

function labelOf(columns: OrderBoardColumn[], id: unknown): string {
  return columns.find((c) => c.subStatus === id)?.label ?? String(id)
}

/** Tablero kanban de una pestaña: columnas en paralelo con arrastrar y soltar entre ellas. */
export function OrderBoard({ columns, workflow, onMove }: OrderBoardProps) {
  const [active, setActive] = useState<OrderCard | null>(null)
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    useSensor(TouchSensor, { activationConstraint: { delay: 200, tolerance: 8 } }),
  )

  const handleDragStart = useCallback((event: DragStartEvent) => {
    setActive((event.active.data.current?.order as OrderCard | undefined) ?? null)
  }, [])

  const handleDragEnd = useCallback(
    (event: DragEndEvent) => {
      setActive(null)
      const order = event.active.data.current?.order as OrderCard | undefined
      const target = event.over?.id as OrderSubStatus | undefined
      if (order && target && target !== order.subStatus) onMove(order, target)
    },
    [onMove],
  )

  const announcements: Announcements = {
    onDragStart: ({ active: a }) => `Moviendo el pedido ${(a.data.current?.order as OrderCard | undefined)?.orderNumber ?? ''}.`,
    onDragOver: ({ over }) => (over ? `Sobre la columna ${labelOf(columns, over.id)}.` : 'Fuera de las columnas.'),
    onDragEnd: ({ over }) => (over ? `Pedido soltado en ${labelOf(columns, over.id)}.` : 'Movimiento cancelado.'),
    onDragCancel: () => 'Movimiento cancelado.',
  }

  return (
    <DndContext
      sensors={sensors}
      onDragStart={handleDragStart}
      onDragEnd={handleDragEnd}
      onDragCancel={() => setActive(null)}
      accessibility={{
        announcements,
        screenReaderInstructions: {
          draggable: 'Usa el menú de acciones de cada pedido para moverlo con el teclado.',
        },
      }}
    >
      <BoardGrid count={columns.length}>
        {columns.map((col) => (
          <BoardColumn key={col.subStatus} column={col} workflow={workflow} onMove={onMove} />
        ))}
      </BoardGrid>
      <DragOverlay dropAnimation={null}>{active ? <OrderCardView order={active} overlay /> : null}</DragOverlay>
    </DndContext>
  )
}

/** Columnas lado a lado; en móvil se desplazan horizontalmente con "snap". */
function BoardGrid({ count, children }: { count: number; children: ReactNode }) {
  return (
    <div className="-mx-4 overflow-x-auto px-4 pb-2 sm:mx-0 sm:px-0">
      <div
        className={cn(
          'grid snap-x snap-mandatory auto-cols-[minmax(17rem,85vw)] grid-flow-col gap-4 sm:auto-cols-[minmax(17rem,1fr)]',
          count === 1 && 'sm:max-w-md',
        )}
      >
        {children}
      </div>
    </div>
  )
}

function BoardColumn({
  column,
  workflow,
  onMove,
}: {
  column: OrderBoardColumn
  workflow: OrderWorkflow | undefined
  onMove: (order: OrderCard, target: OrderSubStatus) => void
}) {
  const money = useMoney()
  const { setNodeRef, isOver, active } = useDroppable({ id: column.subStatus })
  const draggingFromHere = (active?.data.current?.order as OrderCard | undefined)?.subStatus === column.subStatus
  const headingId = `col-${column.subStatus}`
  const hidden = column.count - column.orders.length

  return (
    <section
      aria-labelledby={headingId}
      className={cn(
        'bg-muted/40 flex min-h-64 snap-start flex-col rounded-xl border border-t-4 transition-colors',
        statusTone(column.css).accent,
        isOver && !draggingFromHere && 'bg-primary/5 ring-primary/40 ring-2',
      )}
    >
      <header className="flex items-center justify-between gap-2 px-3 pt-3 pb-2">
        <div className="flex min-w-0 items-center gap-2">
          <StatusDot css={column.css} />
          <h2 id={headingId} className="truncate text-sm font-semibold">
            {column.label}
          </h2>
          <span
            className="bg-background text-muted-foreground rounded-full border px-2 py-0.5 text-xs font-medium tabular-nums"
            aria-label={`${column.count} pedidos`}
          >
            {column.count}
          </span>
        </div>
        {column.count > 0 && (
          <span className="text-muted-foreground text-xs tabular-nums" title="Importe total de la columna">
            {money(column.totalAmount)}
          </span>
        )}
      </header>
      <ul ref={setNodeRef} className="flex flex-1 flex-col gap-2 overflow-y-auto px-3 pb-3 lg:max-h-[calc(100vh-22rem)]">
        {column.orders.length === 0 ? (
          <li
            className={cn(
              'text-muted-foreground flex flex-1 flex-col items-center justify-center gap-2 rounded-lg border border-dashed py-10 text-sm',
              isOver && 'border-primary/50 text-primary',
            )}
          >
            <Inbox className="size-5" aria-hidden />
            {isOver ? 'Suelta aquí' : 'Sin pedidos'}
          </li>
        ) : (
          column.orders.map((order) => (
            <DraggableOrderCard key={order.id} order={order} workflow={workflow} onMove={onMove} />
          ))
        )}
        {hidden > 0 && (
          <li className="text-muted-foreground px-1 pt-1 text-center text-xs">
            Mostrando {column.orders.length} de {column.count}. Usa la búsqueda o las fechas para acotar.
          </li>
        )}
      </ul>
    </section>
  )
}

/** Esqueleto del tablero mientras carga. */
export function OrderBoardSkeleton({ columns = 3 }: { columns?: number }) {
  return (
    <BoardGrid count={columns}>
      {Array.from({ length: columns }, (_, i) => (
        <div key={i} className="bg-muted/40 space-y-3 rounded-xl border p-3" aria-hidden>
          <Skeleton className="h-5 w-32" />
          {Array.from({ length: 3 - (i % 2) }, (_, j) => (
            <div key={j} className="bg-card space-y-3 rounded-lg border p-3">
              <Skeleton className="h-4 w-28" />
              <Skeleton className="h-4 w-40" />
              <div className="flex justify-between">
                <Skeleton className="h-5 w-20" />
                <Skeleton className="h-3 w-16" />
              </div>
            </div>
          ))}
        </div>
      ))}
    </BoardGrid>
  )
}
