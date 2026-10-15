import { useQuery } from '@tanstack/react-query'
import { createFileRoute, Link } from '@tanstack/react-router'
import { ArrowLeft, ChevronDown, Clock, MessagesSquare, Package, Phone, SearchX } from 'lucide-react'
import { useCallback } from 'react'
import { ChannelBadge } from '@/components/shared/channel'
import { EmptyState } from '@/components/shared/empty-state'
import { PageContainer } from '@/components/shared/page-header'
import { QueryError } from '@/components/shared/query-state'
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Label } from '@/components/ui/label'
import { Separator } from '@/components/ui/separator'
import { Skeleton } from '@/components/ui/skeleton'
import { Switch } from '@/components/ui/switch'
import {
  orderDetailQuery,
  orderWorkflowQuery,
  type OrderDetail,
  type OrderSubStatus,
  type OrderWorkflow,
} from '@/features/orders/api'
import { MoveOrderMenu } from '@/features/orders/components/move-order-menu'
import { OrderAdminForm } from '@/features/orders/components/order-admin-form'
import { OrderStepper } from '@/features/orders/components/order-stepper'
import { OrderStatusBadge } from '@/components/shared/order-status'
import { useOrderMover } from '@/features/orders/components/use-order-mover'
import { useNotifyPreference } from '@/features/orders/hooks'
import { conversationHref } from '@/features/orders/links'
import { useMoney } from '@/lib/auth/session'
import { formatDateTime, formatRelative, initials } from '@/lib/format'

export const Route = createFileRoute('/_app/orders/$orderId')({
  head: () => ({ meta: [{ title: 'Detalle del pedido — InstaVende' }] }),
  component: OrderDetailPage,
})

function BackLink() {
  return (
    <Button asChild variant="ghost" size="sm" className="-ml-2 w-fit">
      <Link to="/orders">
        <ArrowLeft /> Pedidos
      </Link>
    </Button>
  )
}

function OrderDetailPage() {
  const { orderId } = Route.useParams()
  const id = Number(orderId)
  const valid = Number.isInteger(id) && id > 0

  return (
    <PageContainer>
      <BackLink />
      {valid ? (
        <OrderDetailContent id={id} />
      ) : (
        <EmptyState icon={SearchX} title="Pedido no encontrado" description="El enlace no corresponde a un pedido válido." />
      )}
    </PageContainer>
  )
}

function OrderDetailContent({ id }: { id: number }) {
  const detail = useQuery(orderDetailQuery(id))
  const workflow = useQuery(orderWorkflowQuery)

  if (detail.isPending) return <OrderDetailSkeleton />
  if (detail.isError) return <QueryError error={detail.error} onRetry={() => void detail.refetch()} />
  return <OrderDetailView order={detail.data} workflow={workflow.data} />
}

function OrderDetailView({ order, workflow }: { order: OrderDetail; workflow: OrderWorkflow | undefined }) {
  const money = useMoney()
  const [notify, setNotify] = useNotifyPreference()
  const { move, isPending } = useOrderMover({ notifyPreference: notify })
  const onMove = useCallback((target: OrderSubStatus) => void move(order, target), [move, order])
  const hasConversation = order.conversationId != null

  return (
    <>
      {/* Cabecera */}
      <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
        <div className="min-w-0 space-y-2">
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="font-mono text-2xl font-semibold tracking-tight sm:text-3xl">{order.orderNumber}</h1>
            <OrderStatusBadge label={order.subStatusLabel} css={order.subStatusCss} />
            <ChannelBadge channel={order.channel} />
          </div>
          <p className="text-muted-foreground text-sm">
            Creado el {formatDateTime(order.createdAt)} · {formatRelative(order.createdAt)}
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {order.conversationId != null && (
            <Button asChild variant="outline">
              <a href={conversationHref(order.conversationId)}>
                <MessagesSquare /> Ir a la conversación
              </a>
            </Button>
          )}
          <MoveOrderMenu
            workflow={workflow}
            current={order.subStatus}
            onMove={onMove}
            trigger={
              <Button disabled={!workflow || isPending}>
                Cambiar estado <ChevronDown />
              </Button>
            }
          />
        </div>
      </div>

      {/* Progreso */}
      <Card>
        <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-3">
          <div className="space-y-1">
            <CardTitle>Progreso del pedido</CardTitle>
            <CardDescription>Estado actual: {order.subStatusLabel}</CardDescription>
          </div>
          {hasConversation && (
            <div className="flex items-center gap-2">
              <Switch id="notify-customer" checked={notify} onCheckedChange={setNotify} />
              <Label htmlFor="notify-customer" className="cursor-pointer text-sm font-normal">
                Avisar al cliente al cambiar de estado
              </Label>
            </div>
          )}
        </CardHeader>
        <CardContent>
          {workflow ? (
            <OrderStepper workflow={workflow} current={order.subStatus} currentLabel={order.subStatusLabel} />
          ) : (
            <Skeleton className="h-14 w-full" />
          )}
        </CardContent>
      </Card>

      <div className="grid gap-6 lg:grid-cols-3">
        {/* Productos y totales */}
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle>Productos</CardTitle>
            <CardDescription>
              {order.items.length} {order.items.length === 1 ? 'línea' : 'líneas'} ·{' '}
              {order.items.reduce((sum, i) => sum + i.quantity, 0)} unidades
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {order.items.length === 0 ? (
              <p className="text-muted-foreground py-6 text-center text-sm">Este pedido no tiene productos.</p>
            ) : (
              <ul className="divide-y">
                {order.items.map((item) => (
                  <li key={item.id} className="flex items-center gap-3 py-3 first:pt-0">
                    <div className="bg-muted flex size-12 shrink-0 items-center justify-center overflow-hidden rounded-lg border">
                      {item.imageUrl ? (
                        <img src={item.imageUrl} alt="" className="size-full object-cover" loading="lazy" />
                      ) : (
                        <Package className="text-muted-foreground size-5" aria-hidden />
                      )}
                    </div>
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm font-medium">{item.productName}</p>
                      {item.variantDescription && (
                        <p className="text-muted-foreground truncate text-xs">{item.variantDescription}</p>
                      )}
                      <p className="text-muted-foreground text-xs tabular-nums">
                        {item.quantity} × {money(item.unitPrice)}
                      </p>
                    </div>
                    <p className="text-sm font-semibold tabular-nums">{money(item.subtotal)}</p>
                  </li>
                ))}
              </ul>
            )}
            <Separator />
            <dl className="ml-auto max-w-xs space-y-1.5 text-sm">
              <div className="flex justify-between gap-4">
                <dt className="text-muted-foreground">Subtotal</dt>
                <dd className="tabular-nums">{money(order.subtotal)}</dd>
              </div>
              {order.discount > 0 && (
                <div className="flex justify-between gap-4">
                  <dt className="text-muted-foreground">Descuento</dt>
                  <dd className="text-success tabular-nums">−{money(order.discount)}</dd>
                </div>
              )}
              <div className="flex justify-between gap-4">
                <dt className="text-muted-foreground">Envío</dt>
                <dd className="tabular-nums">{order.shippingCost > 0 ? money(order.shippingCost) : 'Gratis'}</dd>
              </div>
              <div className="flex justify-between gap-4 border-t pt-2 text-base font-semibold">
                <dt>Total</dt>
                <dd className="font-display tabular-nums">{money(order.total)}</dd>
              </div>
            </dl>
          </CardContent>
        </Card>

        <div className="space-y-6">
          {/* Cliente */}
          <Card>
            <CardHeader>
              <CardTitle>Cliente</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              <div className="flex items-center gap-3">
                <Avatar className="size-10">
                  {order.contact.avatarUrl && <AvatarImage src={order.contact.avatarUrl} alt="" />}
                  <AvatarFallback>{initials(order.contact.name)}</AvatarFallback>
                </Avatar>
                <div className="min-w-0">
                  <p className="truncate font-medium">{order.contact.name}</p>
                  <ChannelBadge channel={order.contact.channel} className="mt-0.5" />
                </div>
              </div>
              {order.contact.phone && (
                <a
                  href={`tel:${order.contact.phone.replace(/[^\d+]/g, '')}`}
                  className="text-muted-foreground hover:text-foreground flex items-center gap-2 text-sm"
                >
                  <Phone className="size-4" aria-hidden />
                  {order.contact.phone}
                </a>
              )}
            </CardContent>
          </Card>

          {/* Entrega y pago */}
          <Card>
            <CardHeader>
              <CardTitle>Entrega y pago</CardTitle>
              <CardDescription>Corrige los datos que registró el vendedor IA.</CardDescription>
            </CardHeader>
            <CardContent>
              <OrderAdminForm order={order} />
            </CardContent>
          </Card>

          {/* Historial */}
          <Card>
            <CardHeader>
              <CardTitle>Historial</CardTitle>
            </CardHeader>
            <CardContent>
              <ol className="space-y-3 text-sm">
                <li className="flex gap-3">
                  <Clock className="text-muted-foreground mt-0.5 size-4 shrink-0" aria-hidden />
                  <div>
                    <p className="font-medium">Pedido registrado</p>
                    <p className="text-muted-foreground text-xs">
                      <time dateTime={order.createdAt}>{formatDateTime(order.createdAt)}</time>
                    </p>
                  </div>
                </li>
                {order.updatedAt !== order.createdAt && (
                  <li className="flex gap-3">
                    <Clock className="text-muted-foreground mt-0.5 size-4 shrink-0" aria-hidden />
                    <div>
                      <p className="font-medium">Última actualización</p>
                      <p className="text-muted-foreground text-xs">
                        <time dateTime={order.updatedAt}>{formatDateTime(order.updatedAt)}</time> ·{' '}
                        {formatRelative(order.updatedAt)}
                      </p>
                    </div>
                  </li>
                )}
              </ol>
            </CardContent>
          </Card>
        </div>
      </div>
    </>
  )
}

function OrderDetailSkeleton() {
  return (
    <div className="space-y-6" aria-hidden>
      <div className="space-y-2">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-4 w-48" />
      </div>
      <Skeleton className="h-32 w-full rounded-xl" />
      <div className="grid gap-6 lg:grid-cols-3">
        <Skeleton className="h-80 rounded-xl lg:col-span-2" />
        <div className="space-y-6">
          <Skeleton className="h-32 rounded-xl" />
          <Skeleton className="h-72 rounded-xl" />
        </div>
      </div>
    </div>
  )
}
