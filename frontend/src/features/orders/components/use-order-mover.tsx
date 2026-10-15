import { useQuery } from '@tanstack/react-query'
import { useCallback } from 'react'
import { useConfirm } from '@/components/shared/confirm-dialog'
import { orderWorkflowQuery, useMoveOrder, type OrderSubStatus, type OrderSubStatusInfo } from '../api'
import { CancelDescription } from './cancel-description'

/** Datos mínimos de un pedido para moverlo (sirve tanto la tarjeta como el detalle). */
export interface MovableOrder {
  id: number
  orderNumber: string
  subStatus: OrderSubStatus
  conversationId?: number | null
}

/**
 * Lógica común para mover pedidos: pide confirmación al cancelar/rechazar (con opción de aviso)
 * y ejecuta la mutación optimista.
 */
export function useOrderMover({ notifyPreference }: { notifyPreference: boolean }) {
  const confirm = useConfirm()
  const { data: workflow } = useQuery(orderWorkflowQuery)
  const mutation = useMoveOrder()
  const { mutate } = mutation

  const findInfo = useCallback(
    (sub: OrderSubStatus): OrderSubStatusInfo | undefined =>
      workflow?.tabs.flatMap((t) => t.columns).find((c) => c.subStatus === sub),
    [workflow],
  )

  const move = useCallback(
    async (order: MovableOrder, targetSub: OrderSubStatus) => {
      if (order.subStatus === targetSub) return
      const target = findInfo(targetSub)
      if (!target) return

      const hasConversation = order.conversationId != null
      let notify = notifyPreference && hasConversation
      if (target.tab === 'Cancelados') {
        const rejecting = target.subStatus === 'Rechazado'
        const choice = { notify: hasConversation }
        const ok = await confirm({
          title: rejecting ? `¿Rechazar el pedido ${order.orderNumber}?` : `¿Cancelar el pedido ${order.orderNumber}?`,
          description: (
            <CancelDescription
              text={
                rejecting
                  ? 'El pedido pasará a «Rechazado» y dejará de contar en tus ingresos.'
                  : 'El pedido pasará a «Cancelado» y dejará de contar en tus ingresos.'
              }
              canNotify={hasConversation}
              defaultNotify={choice.notify}
              onNotifyChange={(value) => {
                choice.notify = value
              }}
            />
          ),
          confirmLabel: rejecting ? 'Rechazar pedido' : 'Cancelar pedido',
          cancelLabel: 'Volver',
          tone: 'destructive',
        })
        if (!ok) return
        notify = choice.notify && hasConversation
      }

      mutate({ id: order.id, orderNumber: order.orderNumber, target, notifyCustomer: notify })
    },
    [confirm, findInfo, mutate, notifyPreference],
  )

  return { move, workflow, isPending: mutation.isPending }
}
