import { Link2, Pencil, Trash2 } from 'lucide-react'
import type { ReactNode } from 'react'
import type { PaymentMethod } from '../../api'
import { paymentFieldLabels, paymentTypeMeta } from '../../meta'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Switch } from '@/components/ui/switch'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { cn } from '@/lib/utils'

interface PaymentMethodCardProps {
  method: PaymentMethod
  handle: ReactNode
  onToggle: (isActive: boolean) => void
  onEdit: () => void
  onDelete: () => void
}

/** Tarjeta de un método de pago con icono según su tipo, datos clave y acciones. */
export function PaymentMethodCard({ method, handle, onToggle, onEdit, onDelete }: PaymentMethodCardProps) {
  const meta = paymentTypeMeta[method.type]
  const labels = paymentFieldLabels(method.type)
  const Icon = meta.icon

  return (
    <Card className={cn('gap-3 px-3 py-4 sm:px-4', !method.isActive && 'bg-muted/40')}>
      <div className="flex items-start gap-3">
        {handle}
        <div
          className={cn(
            'flex size-10 shrink-0 items-center justify-center rounded-lg',
            meta.tone,
            !method.isActive && 'opacity-60',
          )}
        >
          <Icon className="size-5" aria-hidden />
        </div>
        <div className={cn('min-w-0 flex-1', !method.isActive && 'opacity-60')}>
          <h3 className="leading-snug font-medium break-words">{method.name}</h3>
          <p className="text-muted-foreground text-xs">{meta.label}</p>
        </div>
        <div className="flex shrink-0 items-center gap-2">
          <span aria-hidden className="text-muted-foreground hidden text-xs sm:inline">
            {method.isActive ? 'Activo' : 'Inactivo'}
          </span>
          <Switch
            checked={method.isActive}
            onCheckedChange={onToggle}
            aria-label={`Método ${method.name} activo`}
          />
        </div>
      </div>

      <div className={cn('space-y-2 sm:pl-[5.25rem]', !method.isActive && 'opacity-60')}>
        {method.instructions ? (
          <p className="text-muted-foreground line-clamp-3 text-sm break-words whitespace-pre-line">
            {method.instructions}
          </p>
        ) : (
          <p className="text-muted-foreground text-sm italic">Sin instrucciones: agrégalas para que el cliente sepa cómo pagar.</p>
        )}
        {(method.accountNumber || method.accountAlias || method.paymentLink) && (
          <dl className="flex flex-wrap gap-2 text-xs">
            {method.accountNumber && <Detail label={labels.account} value={method.accountNumber} />}
            {method.accountAlias && <Detail label={labels.alias} value={method.accountAlias} />}
            {method.paymentLink && (
              <div className="bg-muted inline-flex max-w-full items-center gap-1 rounded-md px-2 py-1">
                <dt className="sr-only">Link de pago</dt>
                <Link2 className="text-muted-foreground size-3 shrink-0" aria-hidden />
                <dd className="min-w-0">
                  <a
                    href={method.paymentLink}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="text-primary block truncate hover:underline"
                  >
                    {method.paymentLink}
                  </a>
                </dd>
              </div>
            )}
          </dl>
        )}
      </div>

      <div className="flex justify-end gap-1">
        <Tooltip>
          <TooltipTrigger asChild>
            <Button variant="ghost" size="sm" onClick={onEdit} aria-label={`Editar ${method.name}`}>
              <Pencil aria-hidden /> Editar
            </Button>
          </TooltipTrigger>
          <TooltipContent>Editar y ver la vista previa</TooltipContent>
        </Tooltip>
        <Tooltip>
          <TooltipTrigger asChild>
            <Button
              variant="ghost"
              size="icon-sm"
              onClick={onDelete}
              aria-label={`Eliminar ${method.name}`}
              className="hover:text-destructive"
            >
              <Trash2 aria-hidden />
            </Button>
          </TooltipTrigger>
          <TooltipContent>Eliminar</TooltipContent>
        </Tooltip>
      </div>
    </Card>
  )
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div className="bg-muted inline-flex max-w-full items-center gap-1 rounded-md px-2 py-1">
      <dt className="text-muted-foreground shrink-0">{label}:</dt>
      <dd className="truncate font-medium">{value}</dd>
    </div>
  )
}
