import { createFileRoute } from '@tanstack/react-router'
import { metricsQuery } from '@/features/analytics/api'
import { MetricsPage } from '@/features/analytics/components/metrics-page'
import { metricsSearchSchema, resolveMetricsParams } from '@/features/analytics/schemas'

export const Route = createFileRoute('/_app/metrics')({
  validateSearch: metricsSearchSchema,
  head: () => ({ meta: [{ title: 'Métricas — InstaVende' }] }),
  loaderDeps: ({ search }) => resolveMetricsParams(search),
  // Precarga sin bloquear: la página muestra esqueletos o los datos del periodo anterior
  loader: ({ context, deps }) => void context.queryClient.prefetchQuery(metricsQuery(deps)),
  component: MetricsPage,
})
