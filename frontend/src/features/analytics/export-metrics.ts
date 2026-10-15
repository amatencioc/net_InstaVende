import type { MetricsData } from './api'
import { buildCsv, downloadTextFile, percentChange, type CsvCell } from './utils'

const channelLabels: Record<string, string> = {
  WhatsApp: 'WhatsApp',
  Instagram: 'Instagram',
  FacebookMessenger: 'Messenger',
}

function change(current: number, previous: number): CsvCell {
  const c = percentChange(current, previous)
  return c === null ? '' : Math.round(c * 10) / 10
}

/** Descarga un CSV con todas las métricas del periodo (generado en el navegador). */
export function exportMetricsCsv(data: MetricsData, currencyCode: string) {
  const { range, sales, previousSales: ps, bot, previousBot: pb } = data
  const csv = buildCsv([
    {
      title: `Métricas InstaVende: ${range.from} a ${range.to} (comparado con ${range.previousFrom} a ${range.previousTo})`,
      rows: [
        ['Indicador', 'Periodo', 'Periodo anterior', 'Variación %'],
        [`Ingresos (${currencyCode})`, sales.revenue, ps.revenue, change(sales.revenue, ps.revenue)],
        ['Pedidos', sales.orders, ps.orders, change(sales.orders, ps.orders)],
        [
          'Pedidos con ingreso',
          sales.revenueOrders,
          ps.revenueOrders,
          change(sales.revenueOrders, ps.revenueOrders),
        ],
        [
          `Ticket promedio (${currencyCode})`,
          sales.averageTicket,
          ps.averageTicket,
          change(sales.averageTicket, ps.averageTicket),
        ],
        [
          'Conversaciones',
          sales.conversations,
          ps.conversations,
          change(sales.conversations, ps.conversations),
        ],
        [
          'Conversaciones con pedido',
          sales.conversationsWithOrder,
          ps.conversationsWithOrder,
          change(sales.conversationsWithOrder, ps.conversationsWithOrder),
        ],
        [
          'Tasa de conversión %',
          sales.conversionRate,
          ps.conversionRate,
          change(sales.conversionRate, ps.conversionRate),
        ],
        ['Contactos nuevos', sales.newContacts, ps.newContacts, change(sales.newContacts, ps.newContacts)],
        ['Mensajes del bot', bot.botMessages, pb.botMessages, change(bot.botMessages, pb.botMessages)],
        [
          'Mensajes del equipo',
          bot.humanMessages,
          pb.humanMessages,
          change(bot.humanMessages, pb.humanMessages),
        ],
        [
          'Automatización %',
          bot.automationRate,
          pb.automationRate,
          change(bot.automationRate, pb.automationRate),
        ],
        ['Derivaciones a humano', bot.handoffs, pb.handoffs, change(bot.handoffs, pb.handoffs)],
        [
          'Primera respuesta (segundos)',
          bot.averageFirstResponseSeconds,
          pb.averageFirstResponseSeconds,
          bot.averageFirstResponseSeconds != null && pb.averageFirstResponseSeconds != null
            ? change(bot.averageFirstResponseSeconds, pb.averageFirstResponseSeconds)
            : '',
        ],
      ],
    },
    {
      title: 'Embudo de conversión',
      rows: [['Paso', 'Cantidad'], ...data.funnel.map((s): CsvCell[] => [s.label, s.value])],
    },
    {
      title: 'Serie diaria',
      rows: [
        [
          'Fecha',
          `Ingresos (${currencyCode})`,
          'Pedidos',
          'Conversaciones',
          'Mensajes del bot',
          'Mensajes del equipo',
        ],
        ...data.daily.map((d): CsvCell[] => [
          d.date,
          d.revenue,
          d.orders,
          d.conversations,
          d.botMessages,
          d.humanMessages,
        ]),
      ],
    },
    {
      title: 'Ingresos por canal',
      rows: [
        ['Canal', `Ingresos (${currencyCode})`, 'Pedidos', 'Conversaciones'],
        ...data.channels.map((c): CsvCell[] => [
          channelLabels[c.channel] ?? c.channel,
          c.revenue,
          c.orders,
          c.conversations,
        ]),
      ],
    },
    {
      title: 'Productos más vendidos (por unidades)',
      rows: [
        ['Producto', 'Unidades', `Ingresos (${currencyCode})`, 'Pedidos'],
        ...data.topProductsByUnits.map((p): CsvCell[] => [p.name, p.units, p.revenue, p.orders]),
      ],
    },
    {
      title: 'Productos con más ingresos',
      rows: [
        ['Producto', 'Unidades', `Ingresos (${currencyCode})`, 'Pedidos'],
        ...data.topProductsByRevenue.map((p): CsvCell[] => [p.name, p.units, p.revenue, p.orders]),
      ],
    },
    {
      title: 'Mejores clientes',
      rows: [
        ['Cliente', 'Canal', 'Pedidos', `Gasto (${currencyCode})`, 'Último pedido (UTC)'],
        ...data.topCustomers.map((c): CsvCell[] => [
          c.name,
          channelLabels[c.channel] ?? c.channel,
          c.orders,
          c.spent,
          c.lastOrderAt,
        ]),
      ],
    },
    {
      title: 'Pedidos por estado',
      rows: [
        ['Estado', 'Pedidos', `Total (${currencyCode})`],
        ...data.subStatuses.map((s): CsvCell[] => [s.label, s.orders, s.total]),
      ],
    },
  ])
  downloadTextFile(csv, `metricas_${range.from}_${range.to}.csv`)
}
