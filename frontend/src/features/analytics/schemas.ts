import { z } from 'zod'

export const metricsPeriods = ['7d', '30d', '90d', 'custom'] as const
export type MetricsPeriod = (typeof metricsPeriods)[number]

/** Parámetros de búsqueda de /metrics. Valores inválidos vuelven al periodo por defecto. */
export const metricsSearchSchema = z.object({
  period: z.enum(metricsPeriods).default('7d').catch('7d'),
  from: z.iso.date().optional().catch(undefined),
  to: z.iso.date().optional().catch(undefined),
})

export type MetricsSearch = z.infer<typeof metricsSearchSchema>

/** Parámetros efectivos enviados a la API (un "custom" incompleto se trata como 7 días). */
export interface MetricsParams {
  period: MetricsPeriod
  from?: string
  to?: string
}

export function resolveMetricsParams(search: MetricsSearch): MetricsParams {
  if (search.period === 'custom') {
    if (search.from && search.to) {
      const [from, to] = search.from <= search.to ? [search.from, search.to] : [search.to, search.from]
      return { period: 'custom', from, to }
    }
    return { period: '7d' }
  }
  return { period: search.period }
}
