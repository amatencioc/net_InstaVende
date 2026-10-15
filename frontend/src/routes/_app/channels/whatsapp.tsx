import { useQuery } from '@tanstack/react-query'
import { createFileRoute, Link } from '@tanstack/react-router'
import { ArrowLeft, ArrowRight, Rocket } from 'lucide-react'
import { Alert } from '@/components/shared/alert'
import { PageContainer, PageHeader } from '@/components/shared/page-header'
import { QueryError } from '@/components/shared/query-state'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { channelsOverviewQuery, findChannel, type ChannelSummary } from '@/features/channels/api'
import { ChannelReadyBanner } from '@/features/channels/components/channel-ready-banner'
import { WhatsAppConnectSteps } from '@/features/channels/components/whatsapp-connect-steps'
import { WhatsAppConnectedCard } from '@/features/channels/components/whatsapp-connected-card'
import { WhatsAppFaq } from '@/features/channels/components/whatsapp-faq'
import { WhatsAppQrPanel } from '@/features/channels/components/whatsapp-qr-panel'
import { useIsAdmin } from '@/lib/auth/session'

export const Route = createFileRoute('/_app/channels/whatsapp')({
  head: () => ({ meta: [{ title: 'WhatsApp — InstaVende' }] }),
  component: WhatsAppPage,
})

function WhatsAppPage() {
  const overview = useQuery({
    ...channelsOverviewQuery,
    // Con el canal conectado se revisa cada 30 s por si la sesión se cierra desde el celular.
    refetchInterval: (query) => (findChannel(query.state.data, 'WhatsApp')?.isActive ? 30_000 : false),
  })
  const whatsApp = findChannel(overview.data, 'WhatsApp')

  return (
    <PageContainer>
      <Link
        to="/channels"
        className="text-muted-foreground hover:text-foreground -mb-2 inline-flex w-fit items-center gap-1 text-sm"
      >
        <ArrowLeft className="size-4" aria-hidden /> Canales
      </Link>
      <PageHeader
        title="WhatsApp"
        description="Conecta tu número para que tu vendedor IA atienda a tus clientes por WhatsApp."
      />

      {overview.isPending ? (
        <WhatsAppSkeleton />
      ) : overview.isError && !overview.data ? (
        <QueryError error={overview.error} onRetry={() => void overview.refetch()} />
      ) : whatsApp &&
        (whatsApp.status === 'Connected' || (whatsApp.isActive && whatsApp.whatsAppMode === 'CloudApi')) ? (
        <>
          {whatsApp.status === 'Connected' && <ChannelReadyBanner />}
          {whatsApp.status === 'NeedsAttention' && whatsApp.statusDetail && (
            <Alert tone="warning" title="Tu canal requiere atención">
              {whatsApp.statusDetail}
            </Alert>
          )}
          <WhatsAppConnectedCard summary={whatsApp} />
        </>
      ) : (
        <ConnectView summary={whatsApp} />
      )}

      <WhatsAppFaq />
    </PageContainer>
  )
}

/** Vista de conexión por QR (canal desconectado o con la sesión caída). */
function ConnectView({ summary }: { summary: ChannelSummary | undefined }) {
  const isAdmin = useIsAdmin()
  const sessionLost = summary?.isActive === true

  return (
    <div className="space-y-6">
      {sessionLost && summary?.statusDetail && (
        <Alert tone="warning" title="Tu WhatsApp necesita reconectarse">
          {summary.statusDetail}
        </Alert>
      )}

      <div className="space-y-1">
        <h2 className="font-display text-xl font-semibold sm:text-2xl">Conecta tu WhatsApp Business</h2>
        <p className="text-muted-foreground text-sm sm:text-base">
          Es el primer paso para empezar a vender con tu vendedor inteligente.
        </p>
      </div>

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,22rem)]">
        <div className="order-2 lg:order-1">
          {isAdmin ? (
            <WhatsAppQrPanel />
          ) : (
            <Alert tone="info" title="Solo un administrador puede conectar WhatsApp">
              Pide al administrador de tu negocio que entre a esta página y escanee el código QR con el
              celular del negocio.
            </Alert>
          )}
        </div>
        <div className="order-1 space-y-6 lg:order-2">
          <WhatsAppConnectSteps />
          <Card className="gap-3 py-5">
            <CardContent className="space-y-3 px-5">
              <div className="flex items-center gap-2">
                <Rocket className="text-brand-accent size-4" aria-hidden />
                <p className="text-sm font-medium">¿Recibes muchos mensajes al día?</p>
              </div>
              <p className="text-muted-foreground text-xs">
                El código QR usa WhatsApp Web. Para volumen alto o máxima estabilidad te recomendamos la API
                oficial de WhatsApp Business (Cloud API).
              </p>
              <Button asChild variant="outline" size="sm">
                <Link to="/channels/meta" search={{ channel: 'WhatsApp' }}>
                  Usar la API oficial <ArrowRight />
                </Link>
              </Button>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  )
}

function WhatsAppSkeleton() {
  return (
    <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,22rem)]">
      <Skeleton className="h-[28rem] w-full rounded-xl" />
      <div className="space-y-3">
        <Skeleton className="h-5 w-40" />
        <Skeleton className="h-20 w-full rounded-xl" />
        <Skeleton className="h-20 w-full rounded-xl" />
        <Skeleton className="h-20 w-full rounded-xl" />
      </div>
    </div>
  )
}
