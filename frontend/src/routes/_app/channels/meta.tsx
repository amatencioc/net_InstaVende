import { useQuery } from '@tanstack/react-query'
import { createFileRoute, Link, useNavigate } from '@tanstack/react-router'
import { ArrowLeft } from 'lucide-react'
import { z } from 'zod'
import { PageContainer, PageHeader } from '@/components/shared/page-header'
import { QueryError } from '@/components/shared/query-state'
import { channelMeta } from '@/components/shared/channel'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import {
  channelsOverviewQuery,
  findChannel,
  type ChannelSummary,
  type ChannelType,
} from '@/features/channels/api'
import { metaChannelInfo } from '@/features/channels/channel-info'
import { MetaConfigForm } from '@/features/channels/components/meta-config-form'
import { MetaGuideCard } from '@/features/channels/components/meta-guide-card'
import { MetaWebhookCard } from '@/features/channels/components/meta-webhook-card'
import { useIsAdmin } from '@/lib/auth/session'
import { cn } from '@/lib/utils'

const channels = ['WhatsApp', 'FacebookMessenger', 'Instagram'] as const satisfies readonly ChannelType[]

const searchSchema = z.object({
  channel: z.enum(channels).optional().catch(undefined),
})

export const Route = createFileRoute('/_app/channels/meta')({
  validateSearch: searchSchema,
  head: () => ({ meta: [{ title: 'Integraciones de Meta — InstaVende' }] }),
  component: MetaChannelsPage,
})

/** Configuración de WhatsApp Cloud API, Messenger e Instagram con una app de Meta for Developers. */
function MetaChannelsPage() {
  const { channel = 'WhatsApp' } = Route.useSearch()
  const navigate = useNavigate({ from: Route.fullPath })
  const overview = useQuery(channelsOverviewQuery)
  const isAdmin = useIsAdmin()

  return (
    <PageContainer>
      <Link
        to="/channels"
        className="text-muted-foreground hover:text-foreground -mb-2 inline-flex w-fit items-center gap-1 text-sm"
      >
        <ArrowLeft className="size-4" aria-hidden /> Canales
      </Link>
      <PageHeader
        title="Integraciones de Meta"
        description="Conecta la API oficial de WhatsApp, Messenger e Instagram con tu app de Meta for Developers."
      />

      <Tabs
        value={channel}
        onValueChange={(value) => void navigate({ search: { channel: value as ChannelType }, replace: true })}
        className="gap-6"
      >
        <div className="-mx-1 overflow-x-auto px-1">
          <TabsList className="h-auto min-w-max">
            {channels.map((c) => {
              const meta = channelMeta[c]
              const active = findChannel(overview.data, c)
              const live = active?.isActive && (c !== 'WhatsApp' || active.whatsAppMode === 'CloudApi')
              return (
                <TabsTrigger key={c} value={c} className="gap-2 px-3 py-1.5">
                  <meta.icon className={cn('size-4', meta.className.split(' ')[0])} aria-hidden />
                  {metaChannelInfo[c].title}
                  {live && (
                    <>
                      <span className="bg-success size-1.5 rounded-full" aria-hidden />
                      <span className="sr-only">(activo)</span>
                    </>
                  )}
                </TabsTrigger>
              )
            })}
          </TabsList>
        </div>

        {channels.map((c) => (
          <TabsContent key={c} value={c}>
            {overview.isPending ? (
              <MetaSkeleton />
            ) : overview.isError && !overview.data ? (
              <QueryError error={overview.error} onRetry={() => void overview.refetch()} />
            ) : (
              <ChannelPanel channel={c} isAdmin={isAdmin} summary={findChannel(overview.data, c)} />
            )}
          </TabsContent>
        ))}
      </Tabs>
    </PageContainer>
  )
}

function ChannelPanel({
  channel,
  summary,
  isAdmin,
}: {
  channel: ChannelType
  summary: ChannelSummary | undefined
  isAdmin: boolean
}) {
  if (!summary) return <QueryError error={new Error('No se encontró la información del canal.')} />
  return (
    <div className="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,22rem)]">
      <div className="space-y-6">
        <MetaConfigForm key={channel} summary={summary} isAdmin={isAdmin} />
        <MetaWebhookCard summary={summary} isAdmin={isAdmin} />
      </div>
      <MetaGuideCard channel={channel} />
    </div>
  )
}

function MetaSkeleton() {
  return (
    <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,22rem)]">
      <div className="space-y-6">
        <Skeleton className="h-[30rem] w-full rounded-xl" />
        <Skeleton className="h-64 w-full rounded-xl" />
      </div>
      <Skeleton className="h-96 w-full rounded-xl" />
    </div>
  )
}
