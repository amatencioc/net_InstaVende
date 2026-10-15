import { useQuery } from '@tanstack/react-query'
import { Plus, Wallet } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import {
  paymentMethodsQuery,
  useDeletePaymentMethod,
  useReorderPaymentMethods,
  useTogglePaymentMethod,
  type PaymentMethod,
} from '../../api'
import { SectionHeader } from '../section-header'
import { SortableList } from '../sortable-list'
import { PaymentImagesSection } from './payment-images-section'
import { PaymentMethodCard } from './payment-method-card'
import { PaymentMethodDialog, type PaymentMethodDialogState } from './payment-method-dialog'
import { useConfirm } from '@/components/shared/confirm-dialog'
import { EmptyState } from '@/components/shared/empty-state'
import { QueryError } from '@/components/shared/query-state'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'

/** Pestaña "Pagos": métodos de pago que ofrece el vendedor e imágenes de apoyo (QR, capturas). */
export function PaymentsPage() {
  const query = useQuery(paymentMethodsQuery)
  const toggle = useTogglePaymentMethod()
  const reorder = useReorderPaymentMethods()
  const remove = useDeletePaymentMethod()
  const confirm = useConfirm()
  const [dialog, setDialog] = useState<PaymentMethodDialogState | null>(null)

  const methods = query.data
  const activeCount = methods?.filter((m) => m.isActive).length ?? 0

  const handleDelete = async (method: PaymentMethod) => {
    const ok = await confirm({
      title: 'Eliminar método de pago',
      description: `Tu vendedor dejará de ofrecer "${method.name}". Esta acción no se puede deshacer.`,
      confirmLabel: 'Eliminar',
      tone: 'destructive',
    })
    if (ok) remove.mutate(method.id)
  }

  let body: ReactNode
  if (query.isPending) body = <MethodsSkeleton />
  else if (query.isError) body = <QueryError error={query.error} onRetry={() => void query.refetch()} />
  else if (methods && methods.length === 0)
    body = (
      <EmptyState
        icon={Wallet}
        title="Agrega tu primer método de pago"
        description="Indica cómo pueden pagarte tus clientes (transferencia, billetera digital, contraentrega…). Tu vendedor compartirá estos datos al cerrar la venta."
        action={
          <Button onClick={() => setDialog({})}>
            <Plus aria-hidden /> Agregar método de pago
          </Button>
        }
      />
    )
  else if (methods)
    body = (
      <div className="flex flex-col gap-3">
        <p className="text-muted-foreground text-sm">
          {activeCount} de {methods.length} {methods.length === 1 ? 'método activo' : 'métodos activos'}
          {methods.length > 1 && ' · Arrastra para cambiar el orden en que tu vendedor los ofrece.'}
        </p>
        <SortableList
          items={methods}
          label="Métodos de pago"
          getLabel={(m) => m.name}
          onReorder={(ids) => reorder.mutate(ids)}
          renderItem={(method, handle) => (
            <PaymentMethodCard
              method={method}
              handle={methods.length > 1 ? handle : null}
              onToggle={(isActive) => toggle.mutate({ id: method.id, isActive })}
              onEdit={() => setDialog({ method })}
              onDelete={() => void handleDelete(method)}
            />
          )}
        />
        {activeCount === 0 && (
          <p className="text-warning text-sm" role="status">
            No tienes métodos activos: tu vendedor derivará a un asesor cuando pregunten cómo pagar.
          </p>
        )}
      </div>
    )

  return (
    <div className="flex flex-col gap-8">
      <section aria-labelledby="payments-title" className="flex flex-col gap-5">
        <SectionHeader
          id="payments-title"
          icon={Wallet}
          title="Métodos de pago"
          description="Define si aceptas pago adelantado, contraentrega o ambos, y los datos que el cliente necesita para pagar."
          actions={
            methods && methods.length > 0 ? (
              <Button onClick={() => setDialog({})}>
                <Plus aria-hidden /> Agregar método
              </Button>
            ) : undefined
          }
        />
        {body}
      </section>

      <PaymentImagesSection />

      <PaymentMethodDialog state={dialog} onOpenChange={(open) => !open && setDialog(null)} />
    </div>
  )
}

function MethodsSkeleton() {
  return (
    <div className="flex flex-col gap-3" aria-busy="true" aria-label="Cargando métodos de pago">
      {Array.from({ length: 2 }, (_, i) => (
        <Card key={i} className="gap-3 px-4 py-4">
          <div className="flex items-center gap-3">
            <Skeleton className="size-10 rounded-lg" />
            <div className="flex-1 space-y-2">
              <Skeleton className="h-5 w-40" />
              <Skeleton className="h-3 w-28" />
            </div>
            <Skeleton className="h-5 w-9 rounded-full" />
          </div>
          <Skeleton className="h-4 w-full" />
          <Skeleton className="h-4 w-2/3" />
        </Card>
      ))}
    </div>
  )
}
