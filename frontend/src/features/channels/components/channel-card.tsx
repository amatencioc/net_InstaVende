import { Link } from '@tanstack/react-router'
import { ArrowRight, CalendarCheck2, QrCode, Settings2, UserRound } from 'lucide-react'
import type { ChannelSummary } from '../api'
import { channelAccountLabel, metaChannelInfo } from '../channel-info'
import { ChannelStatusBadge } from './channel-status-badge'
import { channelMeta } from '@/components/shared/channel'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardFooter, CardHeader } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { formatDate } from '@/lib/format'
import { cn } from '@/lib/utils'

const descriptions: Record<ChannelSummary['channel'], string> = {
  WhatsApp: 'Vende y atiende por WhatsApp con tu número de siempre o con la API oficial.',
  FacebookMessenger: metaChannelInfo.FacebookMessenger.description,
  Instagram: metaChannelInfo.Instagram.description,
}

function modeLabel(summary: ChannelSummary): string | null {
  if (summary.channel !== 'WhatsApp' || !summary.configured) return null
  return summary.whatsAppMode === 'CloudApi' ? 'API oficial (Cloud API)' : 'WhatsApp Web (código QR)'
}

/** Tarjeta de un canal en la página principal de Canales. */
export function ChannelCard({ summary }: { summary: ChannelSummary }) {
  const meta = channelMeta[summary.channel]
  const Icon = meta.icon
  const account = summary.isActive ? channelAccountLabel(summary) : null
  const mode = summary.isActive ? modeLabel(summary) : null
  const titleId = `channel-${summary.channel}-title`

  return (
    <Card className="h-full">
      <CardHeader className="flex flex-row items-start justify-between gap-3">
        <div className="flex min-w-0 items-center gap-3">
          <span
            className={cn('flex size-11 shrink-0 items-center justify-center rounded-xl', meta.className)}
          >
            <Icon className="size-6" aria-hidden />
          </span>
          <div className="min-w-0">
            <h2 id={titleId} className="font-semibold">
              {meta.label}
            </h2>
            {mode && <p className="text-muted-foreground truncate text-xs">{mode}</p>}
          </div>
        </div>
        <ChannelStatusBadge status={summary.status} />
      </CardHeader>

      <CardContent className="flex flex-1 flex-col gap-3 text-sm">
        <p className="text-muted-foreground">{descriptions[summary.channel]}</p>
        {(account || summary.connectedAt) && (
          <dl className="bg-muted/50 space-y-1.5 rounded-lg p-3">
            {account && (
              <div className="flex items-center gap-2">
                <dt className="sr-only">Cuenta</dt>
                <UserRound className="text-muted-foreground size-4 shrink-0" aria-hidden />
                <dd className="truncate font-medium tabular-nums">{account}</dd>
              </div>
            )}
            {summary.connectedAt && (
              <div className="flex items-center gap-2">
                <dt className="sr-only">Conectado desde</dt>
                <CalendarCheck2 className="text-muted-foreground size-4 shrink-0" aria-hidden />
                <dd className="text-muted-foreground">Conectado el {formatDate(summary.connectedAt)}</dd>
              </div>
            )}
          </dl>
        )}
        {summary.status === 'NeedsAttention' && summary.statusDetail && (
          <p className="border-warning/40 bg-warning/10 rounded-lg border p-3 text-xs">
            {summary.statusDetail}
          </p>
        )}
      </CardContent>

      <CardFooter className="flex flex-wrap gap-2">
        <ChannelCardActions summary={summary} />
      </CardFooter>
    </Card>
  )
}

function ChannelCardActions({ summary }: { summary: ChannelSummary }) {
  if (summary.channel === 'WhatsApp') {
    const cloud = summary.whatsAppMode === 'CloudApi' && summary.isActive
    if (cloud)
      return (
        <Button asChild variant="outline" size="sm">
          <Link to="/channels/meta" search={{ channel: 'WhatsApp' }}>
            <Settings2 /> Gestionar Cloud API
          </Link>
        </Button>
      )
    return (
      <>
        <Button asChild size="sm" variant={summary.status === 'Connected' ? 'outline' : 'default'}>
          <Link to="/channels/whatsapp">
            {summary.status === 'Connected' ? <Settings2 /> : <QrCode />}
            {summary.status === 'Connected'
              ? 'Gestionar'
              : summary.isActive
                ? 'Revisar conexión'
                : 'Conectar con QR'}
          </Link>
        </Button>
        {!summary.isActive && (
          <Button asChild size="sm" variant="ghost">
            <Link to="/channels/meta" search={{ channel: 'WhatsApp' }}>
              Usar API oficial <ArrowRight />
            </Link>
          </Button>
        )}
      </>
    )
  }

  return (
    <Button asChild size="sm" variant={summary.isActive ? 'outline' : 'default'}>
      <Link to="/channels/meta" search={{ channel: summary.channel }}>
        <Settings2 /> {summary.isActive ? 'Gestionar' : 'Configurar'}
      </Link>
    </Button>
  )
}

export function ChannelCardSkeleton() {
  return (
    <Card className="h-full">
      <CardHeader className="flex flex-row items-center gap-3">
        <Skeleton className="size-11 rounded-xl" />
        <div className="flex-1 space-y-2">
          <Skeleton className="h-4 w-28" />
          <Skeleton className="h-3 w-20" />
        </div>
      </CardHeader>
      <CardContent className="space-y-2">
        <Skeleton className="h-3 w-full" />
        <Skeleton className="h-3 w-3/4" />
        <Skeleton className="h-14 w-full" />
      </CardContent>
      <CardFooter>
        <Skeleton className="h-8 w-32" />
      </CardFooter>
    </Card>
  )
}
