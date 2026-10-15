import { createFileRoute, Outlet, redirect } from '@tanstack/react-router'
import { useEffect } from 'react'
import { AppSidebar } from '@/components/layout/app-sidebar'
import { AppTopbar } from '@/components/layout/app-topbar'
import { BusinessEventToasts } from '@/components/layout/business-event-toasts'
import { SidebarInset, SidebarProvider } from '@/components/ui/sidebar'
import { sessionQuery } from '@/lib/auth/session'
import { acquireInboxHub } from '@/lib/realtime/inbox-hub'

/** Layout protegido del panel: exige sesión con negocio. */
export const Route = createFileRoute('/_app')({
  beforeLoad: async ({ context, location }) => {
    const session = await context.queryClient.ensureQueryData(sessionQuery)
    if (!session) throw redirect({ to: '/login', search: { redirect: location.href } })
    if (!session.business) throw redirect({ to: '/setup-business' })
  },
  component: AppLayout,
})

function AppLayout() {
  useEffect(() => acquireInboxHub(), [])

  return (
    <SidebarProvider>
      <AppSidebar />
      <SidebarInset>
        <AppTopbar />
        <BusinessEventToasts />
        <div className="flex-1">
          <Outlet />
        </div>
      </SidebarInset>
    </SidebarProvider>
  )
}
