import { useQuery } from '@tanstack/react-query'
import { createFileRoute, Link } from '@tanstack/react-router'
import { ArrowRight, Sparkles } from 'lucide-react'
import { PageContainer, PageHeader } from '@/components/shared/page-header'
import { QueryError } from '@/components/shared/query-state'
import { Button } from '@/components/ui/button'
import { channelsOverviewQuery, findChannel } from '@/features/channels/api'
import { ChannelCard, ChannelCardSkeleton } from '@/features/channels/components/channel-card'
import { ChannelReadyBanner } from '@/features/channels/components/channel-ready-banner'

export const Route = createFileRoute('/_app/channels/')({
  head: () => ({ meta: [{ title: 'Canales — InstaVende' }] }),
  component: ChannelsPage,
})

/** Página principal de Canales: estado de WhatsApp, Messenger e Instagram. */
function ChannelsPage() {
  const overview = useQuery(channelsOverviewQuery)
  const whatsApp = findChannel(overview.data, 'WhatsApp')
  const anyActive = overview.data?.channels.some((c) => c.isActive) ?? false

  return (
    <PageContainer>
      <PageHeader title="Canales" description="Conecta los canales donde tu vendedor IA atiende y vende." />

      {whatsApp?.status === 'Connected' && <ChannelReadyBanner />}

      {overview.data && !anyActive && (
        <section
          aria-labelledby="connect-cta-title"
          className="bg-card flex flex-col gap-4 rounded-2xl border p-5 sm:flex-row sm:items-center sm:justify-between sm:p-6"
        >
          <div className="flex items-start gap-4">
            <div className="bg-primary/10 text-primary flex size-12 shrink-0 items-center justify-center rounded-full">
              <Sparkles className="size-6" aria-hidden />
            </div>
            <div className="space-y-1">
              <h2 id="connect-cta-title" className="font-display text-lg font-semibold">
                Conecta tu WhatsApp Business
              </h2>
              <p className="text-muted-foreground text-sm">
                Es el primer paso para empezar a vender con tu vendedor inteligente.
              </p>
            </div>
          </div>
          <Button asChild className="shrink-0">
            <Link to="/channels/whatsapp">
              Conectar WhatsApp <ArrowRight />
            </Link>
          </Button>
        </section>
      )}

      {overview.isError && !overview.data ? (
        <QueryError error={overview.error} onRetry={() => void overview.refetch()} />
      ) : (
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {overview.data
            ? overview.data.channels.map((summary) => <ChannelCard key={summary.channel} summary={summary} />)
            : Array.from({ length: 3 }, (_, i) => <ChannelCardSkeleton key={i} />)}
        </div>
      )}
    </PageContainer>
  )
}
