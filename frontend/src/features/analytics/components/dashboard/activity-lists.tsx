import { Link } from '@tanstack/react-router'
import type { LucideIcon } from 'lucide-react'
import { ArrowRight, CircleCheck, ShoppingBag } from 'lucide-react'
import type { ReactNode } from 'react'
import { ChannelIcon } from '@/components/shared/channel'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { Button } from '@/components/ui/button'
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { useMoney } from '@/lib/auth/session'
import { formatChatTimestamp, formatDateTime, formatRelative, initials } from '@/lib/format'
import { cn } from '@/lib/utils'
import type { FileRoutesByTo } from '@/routeTree.gen'
import type { DashboardAttentionConversation, DashboardRecentOrder } from '../../api'
import { SubStatusBadge } from '../sub-status-badge'

/** Últimos 5 pedidos con su sub-estado. */
export function RecentOrdersCard({ orders }: { orders: DashboardRecentOrder[] }) {
  const money = useMoney()
  return (
    <ListCard
      title="Pedidos recientes"
      description="Los últimos pedidos confirmados por tus clientes"
      to="/orders"
      linkLabel="Ver pedidos"
      empty={
        orders.length === 0 && (
          <ListEmpty
            icon={ShoppingBag}
            title="Todavía no tienes pedidos"
            description="Cuando tu vendedor cierre una venta, el pedido aparecerá aquí."
          />
        )
      }
    >
      {orders.map((o) => (
        <li key={o.id}>
          <Link
            to="/orders"
            className="hover:bg-muted/60 focus-visible:ring-ring/50 flex items-center gap-3 rounded-lg px-2 py-2.5 outline-none focus-visible:ring-[3px]"
          >
            <ChannelIcon channel={o.channel} className="size-8" />
            <div className="min-w-0 flex-1">
              <div className="flex items-center gap-2">
                <span className="truncate text-sm font-medium">{o.customerName}</span>
                <SubStatusBadge
                  label={o.subStatusLabel}
                  css={o.subStatusCss}
                  className="hidden sm:inline-flex"
                />
              </div>
              <p className="text-muted-foreground truncate text-xs">
                <span className="font-mono">{o.orderNumber}</span> ·{' '}
                <time dateTime={o.createdAt} title={formatDateTime(o.createdAt)}>
                  {formatRelative(o.createdAt)}
                </time>
              </p>
            </div>
            <div className="flex flex-col items-end gap-1">
              <span className="text-sm font-semibold tabular-nums">{money(o.total)}</span>
              <SubStatusBadge label={o.subStatusLabel} css={o.subStatusCss} className="sm:hidden" />
            </div>
          </Link>
        </li>
      ))}
    </ListCard>
  )
}

/** Conversaciones que necesitan a una persona: esperando humano o con mensajes sin leer. */
export function AttentionCard({ conversations }: { conversations: DashboardAttentionConversation[] }) {
  return (
    <ListCard
      title="Requieren atención"
      description="Clientes que esperan respuesta de tu equipo"
      to="/inbox"
      linkLabel="Abrir mensajes"
      empty={
        conversations.length === 0 && (
          <ListEmpty
            icon={CircleCheck}
            tone="success"
            title="Todo al día"
            description="Tu vendedor IA está atendiendo todas las conversaciones."
          />
        )
      }
    >
      {conversations.map((c) => {
        const waiting = c.status === 'WaitingHuman'
        return (
          <li key={c.id}>
            <Link
              to="/inbox"
              className="hover:bg-muted/60 focus-visible:ring-ring/50 flex items-center gap-3 rounded-lg px-2 py-2.5 outline-none focus-visible:ring-[3px]"
            >
              <div className="relative">
                <Avatar className="size-9">
                  <AvatarFallback className="text-xs">{initials(c.contactName)}</AvatarFallback>
                </Avatar>
                <ChannelIcon
                  channel={c.channel}
                  className="ring-card absolute -right-1 -bottom-1 size-4.5 ring-2 [&_svg]:size-2.5"
                />
              </div>
              <div className="min-w-0 flex-1">
                <div className="flex items-center gap-2">
                  <span className="truncate text-sm font-medium">{c.contactName}</span>
                  <span
                    className={cn(
                      'shrink-0 rounded-full px-2 py-0.5 text-[11px] font-medium',
                      waiting ? 'bg-warning/15 text-warning' : 'bg-info/10 text-info',
                    )}
                  >
                    {waiting ? 'Espera a una persona' : `${c.unreadCount} sin leer`}
                  </span>
                </div>
                <p className="text-muted-foreground truncate text-xs">
                  {c.lastMessagePreview || 'Sin mensajes'}
                </p>
              </div>
              {c.lastMessageAt && (
                <time
                  dateTime={c.lastMessageAt}
                  title={formatDateTime(c.lastMessageAt)}
                  className="text-muted-foreground shrink-0 self-start text-xs"
                >
                  {formatChatTimestamp(c.lastMessageAt)}
                </time>
              )}
            </Link>
          </li>
        )
      })}
    </ListCard>
  )
}

interface ListCardProps {
  title: string
  description: string
  to: keyof FileRoutesByTo
  linkLabel: string
  empty: ReactNode
  children: ReactNode
}

function ListCard({ title, description, to, linkLabel, empty, children }: ListCardProps) {
  return (
    <Card className="gap-3">
      <CardHeader>
        <CardTitle>{title}</CardTitle>
        <CardDescription>{description}</CardDescription>
        <CardAction>
          <Button asChild variant="ghost" size="sm" className="-mr-2">
            <Link to={to}>
              {linkLabel} <ArrowRight aria-hidden />
            </Link>
          </Button>
        </CardAction>
      </CardHeader>
      <CardContent className="flex-1 px-4">{empty || <ul className="divide-y">{children}</ul>}</CardContent>
    </Card>
  )
}

function ListEmpty({
  icon: Icon,
  title,
  description,
  tone = 'primary',
}: {
  icon: LucideIcon
  title: string
  description: string
  tone?: 'primary' | 'success'
}) {
  return (
    <div className="flex h-full min-h-48 flex-col items-center justify-center gap-2 rounded-xl border border-dashed px-4 py-8 text-center">
      <div
        className={cn(
          'flex size-10 items-center justify-center rounded-full',
          tone === 'success' ? 'bg-success/10 text-success' : 'bg-primary/10 text-primary',
        )}
      >
        <Icon className="size-5" aria-hidden />
      </div>
      <p className="text-sm font-medium">{title}</p>
      <p className="text-muted-foreground max-w-xs text-xs">{description}</p>
    </div>
  )
}
