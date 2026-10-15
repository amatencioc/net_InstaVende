import { Check, Copy, ShoppingBag } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import type { InboxConversationDetail } from '../api'
import { agentName, contactName, contactPhone, orderBadgeClass } from '../meta'
import { ContactAvatar, LabelDot, StatusBadge } from './bits'
import { ChannelBadge } from '@/components/shared/channel'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { useMoney, useSession } from '@/lib/auth/session'
import { formatDate, formatDateTime, formatNumber, formatRelative } from '@/lib/format'
import { cn } from '@/lib/utils'

/** Ficha del contacto: datos, métricas de compra y últimos pedidos. */
export function ContactPanel({ detail }: { detail: InboxConversationDetail }) {
  const money = useMoney()
  const { user } = useSession()
  const contact = detail.contact
  const name = contactName(contact, detail.channel)
  const phone = contactPhone(contact, detail.channel)

  return (
    <div className="space-y-6 p-4">
      <div className="flex flex-col items-center gap-3 pt-2 text-center">
        <ContactAvatar name={name} avatarUrl={contact.avatarUrl} size="xl" />
        <div className="min-w-0 space-y-1.5">
          <p className="truncate text-base font-semibold">{name}</p>
          <ChannelBadge channel={detail.channel} />
        </div>
      </div>

      <dl className="space-y-3 text-sm">
        {phone && (
          <InfoRow label="Teléfono">
            <span className="flex items-center gap-1">
              <span className="truncate tabular-nums">{phone}</span>
              <CopyButton value={phone} label="Copiar teléfono" />
            </span>
          </InfoRow>
        )}
        {!phone && (
          <InfoRow label="ID del canal">
            <span className="flex items-center gap-1">
              <span className="truncate font-mono text-xs">{contact.externalId}</span>
              <CopyButton value={contact.externalId} label="Copiar identificador" />
            </span>
          </InfoRow>
        )}
        <InfoRow label="Cliente desde">{formatDate(contact.firstSeenAt)}</InfoRow>
        <InfoRow label="Última actividad">{formatRelative(contact.lastSeenAt)}</InfoRow>
      </dl>

      <div className="grid grid-cols-3 gap-2">
        <Metric label="Compras" value={formatNumber(contact.totalPurchases)} />
        <Metric label="Total gastado" value={money(contact.totalSpent)} />
        <Metric
          label="Última compra"
          value={contact.lastPurchaseAt ? formatDate(contact.lastPurchaseAt) : '—'}
        />
      </div>

      <section aria-labelledby="contact-orders">
        <h3
          id="contact-orders"
          className="text-muted-foreground mb-2 text-xs font-medium tracking-wide uppercase"
        >
          Últimos pedidos
        </h3>
        {contact.recentOrders.length === 0 ? (
          <div className="text-muted-foreground flex flex-col items-center gap-2 rounded-lg border border-dashed px-4 py-6 text-center text-sm">
            <ShoppingBag className="size-5" aria-hidden />
            Aún no tiene pedidos.
          </div>
        ) : (
          <ul className="divide-y rounded-lg border">
            {contact.recentOrders.map((order) => (
              <li key={order.id} className="space-y-1 px-3 py-2.5">
                <div className="flex items-center justify-between gap-2">
                  <span className="truncate font-mono text-xs font-medium">{order.orderNumber}</span>
                  <span className="text-sm font-semibold tabular-nums">{money(order.total)}</span>
                </div>
                <div className="flex items-center justify-between gap-2">
                  <span
                    className={cn(
                      'rounded-full px-2 py-0.5 text-[11px] font-medium whitespace-nowrap',
                      orderBadgeClass(order.subStatus),
                    )}
                  >
                    {order.statusLabel}
                  </span>
                  <time dateTime={order.createdAt} className="text-muted-foreground text-xs">
                    {formatDate(order.createdAt)}
                  </time>
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section aria-labelledby="contact-conversation">
        <h3
          id="contact-conversation"
          className="text-muted-foreground mb-2 text-xs font-medium tracking-wide uppercase"
        >
          Conversación
        </h3>
        <dl className="space-y-3 text-sm">
          <InfoRow label="Estado">
            <StatusBadge status={detail.status} />
          </InfoRow>
          {detail.assignedAgent && (
            <InfoRow label="Atendida por">
              {detail.assignedAgent.id === user.id ? 'Ti' : agentName(detail.assignedAgent)}
            </InfoRow>
          )}
          {detail.label && (
            <InfoRow label="Etiqueta">
              <span className="inline-flex items-center gap-1.5">
                <LabelDot color={detail.label.color} />
                {detail.label.name}
              </span>
            </InfoRow>
          )}
          <InfoRow label="Iniciada">{formatDateTime(detail.createdAt)}</InfoRow>
          {detail.resolvedAt && <InfoRow label="Resuelta">{formatDateTime(detail.resolvedAt)}</InfoRow>}
        </dl>
      </section>
    </div>
  )
}

export function ContactPanelSkeleton() {
  return (
    <div className="space-y-6 p-4" role="status" aria-label="Cargando contacto">
      <div className="flex flex-col items-center gap-3 pt-2">
        <Skeleton className="size-16 rounded-full" />
        <Skeleton className="h-5 w-32" />
        <Skeleton className="h-5 w-20 rounded-full" />
      </div>
      <div className="space-y-3">
        <Skeleton className="h-4 w-full" />
        <Skeleton className="h-4 w-4/5" />
      </div>
      <div className="grid grid-cols-3 gap-2">
        <Skeleton className="h-16" />
        <Skeleton className="h-16" />
        <Skeleton className="h-16" />
      </div>
      <Skeleton className="h-32 w-full" />
    </div>
  )
}

function InfoRow({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex items-center justify-between gap-3">
      <dt className="text-muted-foreground shrink-0">{label}</dt>
      <dd className="min-w-0 truncate text-right font-medium">{children}</dd>
    </div>
  )
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="bg-muted/50 min-w-0 rounded-lg px-2 py-2.5 text-center">
      <p className="truncate text-sm font-semibold tabular-nums" title={value}>
        {value}
      </p>
      <p className="text-muted-foreground mt-0.5 truncate text-[11px]">{label}</p>
    </div>
  )
}

function CopyButton({ value, label }: { value: string; label: string }) {
  const [copied, setCopied] = useState(false)
  return (
    <Button
      variant="ghost"
      size="icon-xs"
      aria-label={copied ? 'Copiado' : label}
      title={copied ? 'Copiado' : label}
      onClick={() => {
        void navigator.clipboard?.writeText(value).then(() => {
          setCopied(true)
          setTimeout(() => setCopied(false), 1500)
        })
      }}
    >
      {copied ? <Check className="text-success" /> : <Copy />}
    </Button>
  )
}
