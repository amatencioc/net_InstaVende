import { Package, Users } from 'lucide-react'
import { useState } from 'react'
import type { LucideIcon } from 'lucide-react'
import { ChannelIcon } from '@/components/shared/channel'
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group'
import { useMoney } from '@/lib/auth/session'
import { formatDateTime, formatNumber, formatRelative } from '@/lib/format'
import type { MetricsData } from '../../api'

type ProductSort = 'units' | 'revenue'

/** Top 10 de productos por unidades o por ingresos. */
export function TopProductsCard({
  byUnits,
  byRevenue,
}: {
  byUnits: MetricsData['topProductsByUnits']
  byRevenue: MetricsData['topProductsByRevenue']
}) {
  const money = useMoney()
  const [sort, setSort] = useState<ProductSort>('units')
  const rows = sort === 'units' ? byUnits : byRevenue
  const max = Math.max(1, ...rows.map((r) => (sort === 'units' ? r.units : r.revenue)))

  return (
    <Card className="gap-3">
      <CardHeader>
        <CardTitle>Productos más vendidos</CardTitle>
        <CardDescription>Top 10 del periodo (pedidos no cancelados)</CardDescription>
        <CardAction>
          <ToggleGroup
            type="single"
            variant="outline"
            size="sm"
            value={sort}
            onValueChange={(v) => v && setSort(v as ProductSort)}
            aria-label="Ordenar productos por"
          >
            <ToggleGroupItem value="units" className="px-2.5">
              Unidades
            </ToggleGroupItem>
            <ToggleGroupItem value="revenue" className="px-2.5">
              Ingresos
            </ToggleGroupItem>
          </ToggleGroup>
        </CardAction>
      </CardHeader>
      <CardContent className="px-2 sm:px-4">
        {rows.length === 0 ? (
          <RankingEmpty icon={Package} text="Cuando vendas productos verás aquí tus favoritos." />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-8 text-center">#</TableHead>
                <TableHead>Producto</TableHead>
                <TableHead className="text-right">Unidades</TableHead>
                <TableHead className="text-right">Ingresos</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((p, i) => {
                const share = ((sort === 'units' ? p.units : p.revenue) / max) * 100
                return (
                  <TableRow key={p.productId}>
                    <TableCell className="text-muted-foreground text-center tabular-nums">{i + 1}</TableCell>
                    <TableCell className="w-full max-w-0">
                      <div className="truncate font-medium" title={p.name}>
                        {p.name}
                      </div>
                      <div className="bg-muted mt-1 h-1 overflow-hidden rounded-full" aria-hidden>
                        <div className="bg-primary h-full rounded-full" style={{ width: `${share}%` }} />
                      </div>
                    </TableCell>
                    <TableCell className="text-right tabular-nums">{formatNumber(p.units)}</TableCell>
                    <TableCell className="text-right font-medium tabular-nums">{money(p.revenue)}</TableCell>
                  </TableRow>
                )
              })}
            </TableBody>
          </Table>
        )}
      </CardContent>
    </Card>
  )
}

/** Top 10 de clientes por gasto en el periodo. */
export function TopCustomersCard({ customers }: { customers: MetricsData['topCustomers'] }) {
  const money = useMoney()
  return (
    <Card className="gap-3">
      <CardHeader>
        <CardTitle>Mejores clientes</CardTitle>
        <CardDescription>Top 10 por gasto en el periodo</CardDescription>
      </CardHeader>
      <CardContent className="px-2 sm:px-4">
        {customers.length === 0 ? (
          <RankingEmpty icon={Users} text="Tus clientes con más compras aparecerán aquí." />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-8 text-center">#</TableHead>
                <TableHead>Cliente</TableHead>
                <TableHead className="text-right">Pedidos</TableHead>
                <TableHead className="text-right">Gasto</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {customers.map((c, i) => (
                <TableRow key={c.contactId}>
                  <TableCell className="text-muted-foreground text-center tabular-nums">{i + 1}</TableCell>
                  <TableCell className="w-full max-w-0">
                    <div className="flex min-w-0 items-center gap-2">
                      <ChannelIcon channel={c.channel} className="size-5 [&_svg]:size-3" />
                      <div className="min-w-0">
                        <div className="truncate font-medium" title={c.name}>
                          {c.name}
                        </div>
                        <div
                          className="text-muted-foreground truncate text-xs"
                          title={formatDateTime(c.lastOrderAt)}
                        >
                          Último pedido {formatRelative(c.lastOrderAt)}
                        </div>
                      </div>
                    </div>
                  </TableCell>
                  <TableCell className="text-right tabular-nums">{formatNumber(c.orders)}</TableCell>
                  <TableCell className="text-right font-medium tabular-nums">{money(c.spent)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>
    </Card>
  )
}

function RankingEmpty({ icon: Icon, text }: { icon: LucideIcon; text: string }) {
  return (
    <div className="flex flex-col items-center gap-2 rounded-xl border border-dashed px-4 py-10 text-center">
      <Icon className="text-muted-foreground size-6" aria-hidden />
      <p className="text-muted-foreground text-sm">{text}</p>
    </div>
  )
}
