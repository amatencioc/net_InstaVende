import { keepPreviousData, queryOptions } from '@tanstack/react-query'
import { api, unwrap, type Schemas } from '@/lib/api/client'
import type { MetricsParams } from './schemas'

export type DashboardData = Schemas['DashboardDto']
export type DashboardKpis = Schemas['DashboardKpisDto']
export type DashboardSetupStep = Schemas['DashboardSetupStepDto']
export type DashboardRecentOrder = Schemas['DashboardRecentOrderDto']
export type DashboardAttentionConversation = Schemas['DashboardAttentionConversationDto']
export type MetricsData = Schemas['MetricsDto']
export type MetricsTopProduct = Schemas['MetricsTopProductDto']
export type ChannelType = Schemas['ChannelType']
export type OrderSubStatus = Schemas['OrderSubStatus']

export const analyticsKeys = {
  all: ['analytics'] as const,
  dashboard: () => [...analyticsKeys.all, 'dashboard'] as const,
  metrics: (params: MetricsParams) => [...analyticsKeys.all, 'metrics', params] as const,
}

/** Resumen de inicio. Se refresca cada minuto mientras la página está abierta. */
export const dashboardQuery = queryOptions({
  queryKey: analyticsKeys.dashboard(),
  queryFn: () => unwrap(api.GET('/api/dashboard')),
  refetchInterval: 60_000,
})

/** Métricas de un periodo; mantiene los datos anteriores mientras carga el nuevo periodo. */
export function metricsQuery(params: MetricsParams) {
  return queryOptions({
    queryKey: analyticsKeys.metrics(params),
    queryFn: () =>
      unwrap(
        api.GET('/api/metrics', {
          params: { query: { period: params.period, from: params.from, to: params.to } },
        }),
      ),
    placeholderData: keepPreviousData,
    staleTime: 60_000,
  })
}
