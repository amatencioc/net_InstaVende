import { useQuery } from '@tanstack/react-query'
import { MapPin, Pencil, Plus, Trash2, Truck } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import {
  deliveryZonesQuery,
  useDeleteDeliveryZone,
  useReorderDeliveryZones,
  useToggleDeliveryZone,
  type DeliveryZone,
} from '../../api'
import { SectionHeader } from '../section-header'
import { SortableList } from '../sortable-list'
import { DeliveryZoneDialog, type DeliveryZoneDialogState } from './delivery-zone-dialog'
import { useConfirm } from '@/components/shared/confirm-dialog'
import { EmptyState } from '@/components/shared/empty-state'
import { QueryError } from '@/components/shared/query-state'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { Switch } from '@/components/ui/switch'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { useMoney } from '@/lib/auth/session'
import { cn } from '@/lib/utils'

/** Pestaña "Entrega": zonas, costos y condiciones de envío que informa el vendedor. */
export function DeliveryPage() {
  const query = useQuery(deliveryZonesQuery)
  const toggle = useToggleDeliveryZone()
  const reorder = useReorderDeliveryZones()
  const remove = useDeleteDeliveryZone()
  const confirm = useConfirm()
  const [dialog, setDialog] = useState<DeliveryZoneDialogState | null>(null)

  const zones = query.data
  const activeCount = zones?.filter((z) => z.isActive).length ?? 0

  const handleDelete = async (zone: DeliveryZone) => {
    const ok = await confirm({
      title: 'Eliminar zona de entrega',
      description: `Tu vendedor dejará de ofrecer envíos a "${zone.name}". Esta acción no se puede deshacer.`,
      confirmLabel: 'Eliminar',
      tone: 'destructive',
    })
    if (ok) remove.mutate(zone.id)
  }

  let body: ReactNode
  if (query.isPending) body = <ZonesSkeleton />
  else if (query.isError) body = <QueryError error={query.error} onRetry={() => void query.refetch()} />
  else if (zones && zones.length === 0)
    body = (
      <EmptyState
        icon={MapPin}
        title="Aún no tienes zonas de entrega"
        description="Agrega los lugares a los que envías y cuánto cuesta el envío. Sin zonas, tu vendedor derivará a un asesor cuando pregunten por envíos."
        action={
          <Button onClick={() => setDialog({})}>
            <Plus aria-hidden /> Agregar zona de entrega
          </Button>
        }
      />
    )
  else if (zones)
    body = (
      <div className="flex flex-col gap-3">
        <p className="text-muted-foreground text-sm">
          {activeCount} de {zones.length} {zones.length === 1 ? 'zona activa' : 'zonas activas'}
          {zones.length > 1 && ' · Arrastra para cambiar el orden en que tu vendedor las ofrece.'}
        </p>
        <SortableList
          items={zones}
          label="Zonas de entrega"
          getLabel={(z) => z.name}
          onReorder={(ids) => reorder.mutate(ids)}
          renderItem={(zone, handle) => (
            <ZoneRow
              zone={zone}
              handle={zones.length > 1 ? handle : null}
              onToggle={(isActive) => toggle.mutate({ id: zone.id, isActive })}
              onEdit={() => setDialog({ zone })}
              onDelete={() => void handleDelete(zone)}
            />
          )}
        />
      </div>
    )

  return (
    <section aria-labelledby="delivery-title" className="flex flex-col gap-5">
      <SectionHeader
        id="delivery-title"
        icon={Truck}
        title="Configuración de entrega"
        description="Define las zonas a las que envías, su costo y condiciones. Tu vendedor usará esta información al cerrar cada venta."
        actions={
          zones && zones.length > 0 ? (
            <Button onClick={() => setDialog({})}>
              <Plus aria-hidden /> Agregar zona
            </Button>
          ) : undefined
        }
      />
      {body}
      <DeliveryZoneDialog state={dialog} onOpenChange={(open) => !open && setDialog(null)} />
    </section>
  )
}

function CostBadge({ cost }: { cost: number | null }) {
  const money = useMoney()
  if (cost === null)
    return (
      <span className="bg-muted text-muted-foreground rounded-full px-2 py-0.5 text-xs font-medium">A coordinar</span>
    )
  if (cost === 0)
    return <span className="bg-success/10 text-success rounded-full px-2 py-0.5 text-xs font-medium">Gratis</span>
  return (
    <span className="bg-primary/10 text-primary rounded-full px-2 py-0.5 text-xs font-semibold tabular-nums">
      {money(cost)}
    </span>
  )
}

function ZoneRow({
  zone,
  handle,
  onToggle,
  onEdit,
  onDelete,
}: {
  zone: DeliveryZone
  handle: ReactNode
  onToggle: (isActive: boolean) => void
  onEdit: () => void
  onDelete: () => void
}) {
  return (
    <Card className={cn('flex-row items-start gap-3 px-3 py-3 sm:items-center sm:px-4', !zone.isActive && 'bg-muted/40')}>
      {handle}
      <div className={cn('min-w-0 flex-1 space-y-1', !zone.isActive && 'opacity-60')}>
        <div className="flex flex-wrap items-center gap-2">
          <h3 className="font-medium break-words">{zone.name}</h3>
          <CostBadge cost={zone.cost} />
        </div>
        {zone.description && (
          <p className="text-muted-foreground line-clamp-2 text-sm break-words whitespace-pre-line">
            {zone.description}
          </p>
        )}
      </div>
      <div className="flex shrink-0 flex-col-reverse items-end gap-2 sm:flex-row sm:items-center">
        <div className="flex gap-1">
          <Tooltip>
            <TooltipTrigger asChild>
              <Button variant="ghost" size="icon-sm" onClick={onEdit} aria-label={`Editar zona ${zone.name}`}>
                <Pencil aria-hidden />
              </Button>
            </TooltipTrigger>
            <TooltipContent>Editar</TooltipContent>
          </Tooltip>
          <Tooltip>
            <TooltipTrigger asChild>
              <Button
                variant="ghost"
                size="icon-sm"
                onClick={onDelete}
                aria-label={`Eliminar zona ${zone.name}`}
                className="hover:text-destructive"
              >
                <Trash2 aria-hidden />
              </Button>
            </TooltipTrigger>
            <TooltipContent>Eliminar</TooltipContent>
          </Tooltip>
        </div>
        <div className="flex items-center gap-2">
          <span aria-hidden className="text-muted-foreground hidden w-14 text-right text-xs sm:inline">
            {zone.isActive ? 'Activa' : 'Inactiva'}
          </span>
          <Switch
            checked={zone.isActive}
            onCheckedChange={onToggle}
            aria-label={`Zona ${zone.name} activa`}
          />
        </div>
      </div>
    </Card>
  )
}

function ZonesSkeleton() {
  return (
    <div className="flex flex-col gap-3" aria-busy="true" aria-label="Cargando zonas de entrega">
      {Array.from({ length: 3 }, (_, i) => (
        <Card key={i} className="flex-row items-center gap-3 px-4 py-4">
          <Skeleton className="size-6" />
          <div className="flex-1 space-y-2">
            <Skeleton className="h-5 w-40" />
            <Skeleton className="h-4 w-2/3" />
          </div>
          <Skeleton className="h-5 w-9 rounded-full" />
        </Card>
      ))}
    </div>
  )
}
