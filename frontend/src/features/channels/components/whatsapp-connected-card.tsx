import { Link } from '@tanstack/react-router'
import {
  Bot,
  FlaskConical,
  Loader2,
  PlugZap,
  RefreshCcw,
  Settings2,
  ShieldCheck,
  Unplug,
  type LucideIcon,
} from 'lucide-react'
import { toast } from 'sonner'
import {
  useDisconnectChannel,
  useDisconnectWhatsApp,
  useTestChannel,
  type ChannelActionResult,
  type ChannelSummary,
} from '../api'
import { ChannelStatusBadge } from './channel-status-badge'
import { WhatsAppIcon } from '@/components/shared/brand-icons'
import { useConfirm } from '@/components/shared/confirm-dialog'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardFooter } from '@/components/ui/card'
import { useIsAdmin } from '@/lib/auth/session'
import { formatDate } from '@/lib/format'

function notifyAction(result: ChannelActionResult) {
  if (result.ok) toast.success(result.message)
  else toast.warning(result.message, { duration: 10_000 })
}

/** Tarjeta del canal WhatsApp activo (QR o Cloud API) con sus acciones. */
export function WhatsAppConnectedCard({ summary }: { summary: ChannelSummary }) {
  const isAdmin = useIsAdmin()
  const confirm = useConfirm()
  const cloud = summary.whatsAppMode === 'CloudApi'
  const test = useTestChannel('WhatsApp')
  const disconnectQr = useDisconnectWhatsApp()
  const disconnectCloud = useDisconnectChannel('WhatsApp')
  const disconnecting = disconnectQr.isPending || disconnectCloud.isPending

  const onTest = () =>
    test.mutate(undefined, {
      onSuccess: (r) => (r.ok ? toast.success(r.message) : toast.error(r.message)),
    })

  const onDisconnect = async () => {
    const ok = await confirm({
      title: '¿Desconectar WhatsApp?',
      description: cloud
        ? 'Tu vendedor dejará de responder por WhatsApp. Tus credenciales de Meta se conservan para que puedas reactivarlo cuando quieras.'
        : 'Tu vendedor dejará de responder por WhatsApp y se cerrará la sesión vinculada. Para volver a conectarlo tendrás que escanear un código QR nuevo. Tus conversaciones se conservan.',
      confirmLabel: 'Desconectar',
      tone: 'destructive',
    })
    if (!ok) return
    if (cloud) disconnectCloud.mutate(undefined, { onSuccess: notifyAction })
    else disconnectQr.mutate(undefined, { onSuccess: notifyAction })
  }

  const highlights: { icon: LucideIcon; title: string; text: string }[] = cloud
    ? [
        {
          icon: ShieldCheck,
          title: 'API oficial de Meta',
          text: 'Conexión estable que no depende de tu celular.',
        },
        {
          icon: Bot,
          title: 'Vendedor respondiendo',
          text: 'Los mensajes entrantes se atienden con IA al instante.',
        },
      ]
    : [
        {
          icon: ShieldCheck,
          title: 'Sesión activa',
          text: 'La sesión se mantiene sola; no necesitas volver a escanear.',
        },
        {
          icon: Bot,
          title: 'Vendedor respondiendo',
          text: 'Los mensajes entrantes se atienden con IA al instante.',
        },
        {
          icon: RefreshCcw,
          title: 'Reconexión automática',
          text: 'Si el servicio se reinicia, la sesión se restablece sola.',
        },
      ]

  return (
    <Card>
      <CardContent className="grid gap-6 lg:grid-cols-[1fr_auto]">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center">
          <span className="bg-whatsapp/10 text-whatsapp flex size-16 shrink-0 items-center justify-center rounded-2xl">
            <WhatsAppIcon className="size-9" />
          </span>
          <div className="min-w-0 space-y-1.5">
            <div className="flex flex-wrap items-center gap-2">
              <h2 className="text-lg font-semibold">
                {cloud ? 'WhatsApp Business API' : 'WhatsApp Business'}
              </h2>
              <ChannelStatusBadge status={summary.status} />
            </div>
            <p className="font-display text-2xl font-semibold tracking-tight tabular-nums">
              {summary.phoneNumber ? `+${summary.phoneNumber}` : 'Número vinculado'}
            </p>
            <p className="text-muted-foreground text-sm">
              {cloud ? 'Conectado con la API oficial (Cloud API)' : 'Vinculado con WhatsApp Web (código QR)'}
              {summary.connectedAt && ` · desde el ${formatDate(summary.connectedAt)}`}
            </p>
          </div>
        </div>
        <ul className="grid gap-3 sm:grid-cols-3 lg:w-80 lg:grid-cols-1">
          {highlights.map((h) => (
            <li key={h.title} className="flex gap-3">
              <h.icon className="text-primary mt-0.5 size-4 shrink-0" aria-hidden />
              <div>
                <p className="text-sm font-medium">{h.title}</p>
                <p className="text-muted-foreground text-xs">{h.text}</p>
              </div>
            </li>
          ))}
        </ul>
      </CardContent>
      <CardFooter className="flex flex-wrap gap-2 border-t pt-6">
        <Button asChild>
          <Link to="/seller/test">
            <FlaskConical /> Probar vendedor
          </Link>
        </Button>
        {isAdmin && (
          <>
            <Button type="button" variant="outline" onClick={onTest} disabled={test.isPending}>
              {test.isPending ? <Loader2 className="animate-spin" /> : <PlugZap />}
              Probar conexión
            </Button>
            {cloud && (
              <Button asChild variant="outline">
                <Link to="/channels/meta" search={{ channel: 'WhatsApp' }}>
                  <Settings2 /> Credenciales
                </Link>
              </Button>
            )}
            <Button
              type="button"
              variant="ghost"
              className="text-destructive hover:text-destructive sm:ml-auto"
              onClick={() => void onDisconnect()}
              disabled={disconnecting}
            >
              {disconnecting ? <Loader2 className="animate-spin" /> : <Unplug />}
              Desconectar
            </Button>
          </>
        )}
      </CardFooter>
    </Card>
  )
}
