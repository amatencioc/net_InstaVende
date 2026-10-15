import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  CheckCircle2,
  Loader2,
  RefreshCw,
  RotateCcw,
  ScanLine,
  ServerCrash,
  ShieldCheck,
  Unplug,
  type LucideIcon,
} from 'lucide-react'
import { useEffect, useState, type ReactNode } from 'react'
import { toast } from 'sonner'
import {
  channelKeys,
  useResetWhatsApp,
  useRestartWhatsAppService,
  whatsappSessionQuery,
  type ChannelServiceStatus,
  type WhatsAppSessionState,
} from '../api'
import { useNow } from '../use-now'
import { QueryError } from '@/components/shared/query-state'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'
import { cn } from '@/lib/utils'

/**
 * Panel de vinculación por código QR. Consulta el estado de la sesión con un intervalo adaptativo
 * (ver `sessionPollInterval`) y, al conectarse, refresca el resumen de canales para mostrar la
 * vista de canal conectado.
 */
export function WhatsAppQrPanel() {
  const qc = useQueryClient()
  const session = useQuery(whatsappSessionQuery(qc))
  const reset = useResetWhatsApp()
  const connected = session.data?.connected === true

  useEffect(() => {
    if (connected) void qc.invalidateQueries({ queryKey: channelKeys.overview() })
  }, [connected, qc])

  const state = session.data?.state as WhatsAppSessionState | undefined
  const canReset = !!session.data && state !== 'offline' && state !== 'connected'

  return (
    <Card className="gap-4">
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-lg">
          <ScanLine className="text-primary size-5" aria-hidden /> Escanea el código QR
        </CardTitle>
        <CardDescription>
          En tu celular abre WhatsApp › <strong>Dispositivos vinculados</strong> ›{' '}
          <strong>Vincular un dispositivo</strong>.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <div
          aria-live="polite"
          aria-busy={session.isFetching}
          className="flex min-h-80 items-center justify-center"
        >
          {session.isPending ? (
            <StateView icon={Loader2} spin title="Conectando con WhatsApp…" />
          ) : session.isError && !session.data ? (
            <div className="w-full">
              <QueryError error={session.error} onRetry={() => void session.refetch()} />
            </div>
          ) : (
            <SessionBody
              state={state}
              qrDataUrl={session.data.qrDataUrl}
              qrHash={session.data.qrHash}
              qrSecondsLeft={session.data.qrSecondsLeft}
              service={session.data.service}
            />
          )}
        </div>
      </CardContent>
      <CardFooter className="flex-col gap-2 sm:flex-row sm:justify-between">
        <p className="text-muted-foreground text-xs">
          El código se renueva solo cada pocos segundos. Mantén esta página abierta mientras escaneas.
        </p>
        <Button
          type="button"
          variant="outline"
          size="sm"
          disabled={!canReset || reset.isPending}
          onClick={() => reset.mutate(undefined, { onSuccess: (r) => toast.info(r.message) })}
        >
          {reset.isPending ? <Loader2 className="animate-spin" /> : <RefreshCw />}
          Generar QR nuevo
        </Button>
      </CardFooter>
    </Card>
  )
}

interface SessionBodyProps {
  state: WhatsAppSessionState | undefined
  qrDataUrl: string | null
  qrHash: string | null
  qrSecondsLeft: number | null
  service: ChannelServiceStatus
}

function SessionBody({ state, qrDataUrl, qrHash, qrSecondsLeft, service }: SessionBodyProps) {
  switch (state) {
    case 'offline':
      return <ServiceDown service={service} />
    case 'qr':
      return qrDataUrl ? (
        <QrDisplay key={qrHash ?? qrDataUrl.length} dataUrl={qrDataUrl} secondsLeft={qrSecondsLeft} />
      ) : (
        <StateView icon={Loader2} spin title="Generando el código QR…" />
      )
    case 'authenticating':
      return (
        <StateView icon={ShieldCheck} pulse title="Código escaneado. Verificando la sesión…">
          Esto toma unos segundos. No cierres WhatsApp en tu celular.
        </StateView>
      )
    case 'connected':
      return (
        <StateView icon={CheckCircle2} tone="success" title="¡WhatsApp conectado!">
          Estamos activando tu canal…
        </StateView>
      )
    case 'disconnected':
      return (
        <StateView icon={Unplug} tone="warning" title="La sesión se cerró">
          Estamos preparando un código nuevo. Si no aparece en unos segundos, pulsa «Generar QR nuevo».
        </StateView>
      )
    default:
      return (
        <StateView icon={Loader2} spin title="Iniciando WhatsApp Web…">
          Estamos abriendo una sesión segura de WhatsApp Web para tu negocio. La primera vez puede tardar
          hasta un minuto.
        </StateView>
      )
  }
}

/** QR con cuenta regresiva. Se monta de nuevo con cada código (key = hash). */
function QrDisplay({ dataUrl, secondsLeft }: { dataUrl: string; secondsLeft: number | null }) {
  const now = useNow(500)
  const [{ end, total }] = useState(() => {
    const ms = Math.max(1, secondsLeft ?? 20) * 1_000
    return { end: now + ms, total: ms }
  })
  const remaining = Math.max(0, end - now)
  const pct = Math.min(100, (remaining / total) * 100)
  const seconds = Math.ceil(remaining / 1_000)
  const expired = remaining === 0

  return (
    <div className="flex flex-col items-center gap-4">
      <div className="ring-border relative rounded-2xl bg-white p-3 shadow-sm ring-1">
        <img
          src={dataUrl}
          alt="Código QR para vincular tu WhatsApp"
          width={256}
          height={256}
          className={cn('size-56 transition sm:size-64', expired && 'opacity-25 blur-[2px]')}
        />
        {expired && (
          <div className="absolute inset-0 flex flex-col items-center justify-center gap-2 text-center text-sm font-medium text-neutral-800">
            <Loader2 className="size-6 animate-spin" aria-hidden />
            Actualizando código…
          </div>
        )}
      </div>
      <div className="w-56 space-y-1.5 sm:w-64">
        <div
          role="progressbar"
          aria-label="Tiempo restante del código QR"
          aria-valuemin={0}
          aria-valuemax={100}
          aria-valuenow={Math.round(pct)}
          className="bg-muted h-1.5 overflow-hidden rounded-full"
        >
          <div
            className={cn(
              'h-full rounded-full transition-[width] duration-500 ease-linear',
              pct > 50 ? 'bg-success' : pct > 25 ? 'bg-warning' : 'bg-destructive',
            )}
            style={{ width: `${pct}%` }}
          />
        </div>
        <p className="text-muted-foreground text-center text-xs tabular-nums">
          {expired ? 'Generando un código nuevo…' : `El código expira en ${seconds} s`}
        </p>
      </div>
    </div>
  )
}

function ServiceDown({ service }: { service: ChannelServiceStatus }) {
  const restart = useRestartWhatsAppService()

  if (service.restarting || restart.isPending)
    return (
      <StateView icon={Loader2} spin title="Reiniciando el servicio de WhatsApp…">
        El código QR aparecerá en unos segundos.
      </StateView>
    )

  return (
    <StateView icon={ServerCrash} tone="warning" title="El servicio de WhatsApp no responde">
      <span className="block">
        Puede estar iniciándose. Lo volvemos a intentar automáticamente cada pocos segundos.
        {service.consecutiveFailures > 0 && ` Intentos fallidos: ${service.consecutiveFailures}.`}
      </span>
      {service.managed ? (
        <Button
          type="button"
          className="mt-4"
          onClick={() => restart.mutate(undefined, { onSuccess: (r) => toast.success(r.message) })}
        >
          <RotateCcw /> Reiniciar servicio
        </Button>
      ) : (
        <span className="mt-2 block">
          Si continúa, pide al administrador del servidor que revise el servicio de WhatsApp.
        </span>
      )}
    </StateView>
  )
}

const toneClass = {
  default: 'bg-primary/10 text-primary',
  success: 'bg-success/15 text-success',
  warning: 'bg-warning/15 text-warning',
}

function StateView({
  icon: Icon,
  title,
  children,
  spin,
  pulse,
  tone = 'default',
}: {
  icon: LucideIcon
  title: string
  children?: ReactNode
  spin?: boolean
  pulse?: boolean
  tone?: keyof typeof toneClass
}) {
  return (
    <div className="flex max-w-sm flex-col items-center gap-3 py-6 text-center">
      <div className={cn('flex size-14 items-center justify-center rounded-full', toneClass[tone])}>
        <Icon
          className={cn('size-7', spin && 'animate-spin', pulse && 'motion-safe:animate-pulse')}
          aria-hidden
        />
      </div>
      <p className="font-medium">{title}</p>
      {children && <div className="text-muted-foreground text-sm">{children}</div>}
    </div>
  )
}
