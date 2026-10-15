import { useDraggable } from '@dnd-kit/core'
import { Link } from '@tanstack/react-router'
import { Eye, MessagesSquare, MoreHorizontal, Package } from 'lucide-react'
import { memo } from 'react'
import { ChannelIcon } from '@/components/shared/channel'
import { Button } from '@/components/ui/button'
import { DropdownMenuItem } from '@/components/ui/dropdown-menu'
import { useMoney } from '@/lib/auth/session'
import { formatDateTime, formatRelative } from '@/lib/format'
import { cn } from '@/lib/utils'
import type { OrderCard as OrderCardData, OrderSubStatus, OrderWorkflow } from '../api'
import { conversationHref } from '../links'
import { MoveOrderMenu } from './move-order-menu'

interface OrderCardViewProps {
  order: OrderCardData
  workflow?: OrderWorkflow
  onMove?: (order: OrderCardData, target: OrderSubStatus) => void
  /** Vista flotante mientras se arrastra (sin interacción). */
  overlay?: boolean
}

/** Contenido visual de la tarjeta (compartido por la tarjeta arrastrable y el overlay). */
export function OrderCardView({ order, workflow, onMove, overlay }: OrderCardViewProps) {
  const money = useMoney()
  return (
    <div
      className={cn(
        'bg-card text-card-foreground group/card relative rounded-lg border p-3 shadow-xs transition-shadow',
        'focus-within:ring-ring/50 hover:shadow-sm focus-within:ring-[3px]',
        overlay && 'ring-primary/40 rotate-1 cursor-grabbing shadow-lg ring-2',
      )}
    >
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          {overlay ? (
            <p className="truncate font-mono text-sm font-semibold">{order.orderNumber}</p>
          ) : (
            <Link
              to="/orders/$orderId"
              params={{ orderId: String(order.id) }}
              className="truncate font-mono text-sm font-semibold outline-none after:absolute after:inset-0 after:rounded-lg hover:underline"
              aria-label={`Pedido ${order.orderNumber} de ${order.contactName}, ver detalle`}
            >
              {order.orderNumber}
            </Link>
          )}
          <div className="mt-1 flex min-w-0 items-center gap-1.5">
            <ChannelIcon channel={order.channel} className="size-5" />
            <span className="truncate text-sm">{order.contactName}</span>
          </div>
        </div>
        {!overlay && onMove && (
          <MoveOrderMenu
            workflow={workflow}
            current={order.subStatus}
            onMove={(target) => onMove(order, target)}
            header={
              <>
                <DropdownMenuItem asChild>
                  <Link to="/orders/$orderId" params={{ orderId: String(order.id) }}>
                    <Eye /> Ver detalle
                  </Link>
                </DropdownMenuItem>
                {order.conversationId != null && (
                  <DropdownMenuItem asChild>
                    <a href={conversationHref(order.conversationId)}>
                      <MessagesSquare /> Ir a la conversación
                    </a>
                  </DropdownMenuItem>
                )}
              </>
            }
            trigger={
              <Button
                variant="ghost"
                size="icon-sm"
                className="relative z-10 -mt-1 -mr-1 shrink-0 opacity-70 group-hover/card:opacity-100 focus-visible:opacity-100"
                aria-label={`Acciones del pedido ${order.orderNumber}`}
                onPointerDown={(e) => e.stopPropagation()}
              >
                <MoreHorizontal />
              </Button>
            }
          />
        )}
      </div>
      <div className="mt-3 flex items-end justify-between gap-2">
        <div>
          <p className="font-display text-base font-semibold tabular-nums">{money(order.total)}</p>
          <p className="text-muted-foreground flex items-center gap-1 text-xs">
            <Package className="size-3" aria-hidden />
            {order.itemCount} {order.itemCount === 1 ? 'artículo' : 'artículos'}
          </p>
        </div>
        <time dateTime={order.createdAt} title={formatDateTime(order.createdAt)} className="text-muted-foreground text-xs">
          {formatRelative(order.createdAt)}
        </time>
      </div>
    </div>
  )
}

/** Tarjeta arrastrable del tablero (ratón/táctil). El teclado usa el menú "Mover a…". */
export const DraggableOrderCard = memo(function DraggableOrderCard(
  props: Omit<OrderCardViewProps, 'overlay'> & { disabled?: boolean },
) {
  const { order, disabled } = props
  const { setNodeRef, listeners, isDragging } = useDraggable({
    id: order.id,
    data: { order },
    disabled,
  })
  return (
    <li
      ref={setNodeRef}
      {...listeners}
      className={cn('touch-manipulation cursor-grab list-none', isDragging && 'opacity-40')}
    >
      <OrderCardView {...props} />
    </li>
  )
})
