import { createFileRoute } from '@tanstack/react-router'
import { dashboardQuery } from '@/features/analytics/api'
import { DashboardPage } from '@/features/analytics/components/dashboard-page'

export const Route = createFileRoute('/_app/dashboard')({
  head: () => ({ meta: [{ title: 'Inicio — InstaVende' }] }),
  // Precarga sin bloquear la navegación: la página muestra esqueletos mientras llega
  loader: ({ context }) => void context.queryClient.prefetchQuery(dashboardQuery),
  component: DashboardPage,
})
