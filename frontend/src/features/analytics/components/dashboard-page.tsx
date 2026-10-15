import { useQuery } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { format } from 'date-fns'
import { es } from 'date-fns/locale'
import { BarChart3 } from 'lucide-react'
import { PageContainer, PageHeader } from '@/components/shared/page-header'
import { QueryError } from '@/components/shared/query-state'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { useSession } from '@/lib/auth/session'
import { dashboardQuery } from '../api'
import { capitalize } from '../utils'
import { ActivityChart } from './dashboard/activity-chart'
import { AttentionCard, RecentOrdersCard } from './dashboard/activity-lists'
import { ChannelDonut } from './dashboard/channel-donut'
import { KpiSection } from './dashboard/kpi-section'
import { QuickActions } from './dashboard/quick-actions'
import { SetupChecklist } from './dashboard/setup-checklist'

function greeting(date: Date): string {
  const h = date.getHours()
  if (h < 12) return 'Buenos días'
  if (h < 19) return 'Buenas tardes'
  return 'Buenas noches'
}

/** Página de inicio del panel. */
export function DashboardPage() {
  const { user, business } = useSession()
  const { data, error, refetch, isError } = useQuery(dashboardQuery)
  const now = new Date()
  const name = user.firstName?.trim()

  return (
    <PageContainer>
      <PageHeader
        title={`${greeting(now)}${name ? `, ${name}` : ''}`}
        description={
          <>
            {capitalize(format(now, "EEEE, d 'de' MMMM", { locale: es }))} · Así va{' '}
            <span className="text-foreground font-medium">{business.name}</span>
          </>
        }
        actions={
          <Button asChild variant="outline">
            <Link to="/metrics">
              <BarChart3 aria-hidden /> Ver métricas
            </Link>
          </Button>
        }
      />

      {isError && !data ? (
        <QueryError error={error} onRetry={() => void refetch()} />
      ) : (
        <>
          {data && <SetupChecklist setup={data.setup} />}
          <KpiSection data={data} />

          <div className="grid gap-4 lg:grid-cols-3">
            <div className="lg:col-span-2">
              {data ? <ActivityChart daily={data.daily} /> : <Skeleton className="h-[22rem] rounded-xl" />}
            </div>
            {data ? <ChannelDonut channels={data.channels} /> : <Skeleton className="h-[22rem] rounded-xl" />}
          </div>

          <div className="grid gap-4 lg:grid-cols-2">
            {data ? (
              <>
                <RecentOrdersCard orders={data.recentOrders} />
                <AttentionCard conversations={data.attentionConversations} />
              </>
            ) : (
              <>
                <Skeleton className="h-80 rounded-xl" />
                <Skeleton className="h-80 rounded-xl" />
              </>
            )}
          </div>

          <QuickActions />
        </>
      )}
    </PageContainer>
  )
}
