import { useQuery } from '@tanstack/react-query'
import { Plus, Settings2 } from 'lucide-react'
import { useState } from 'react'
import { countsQuery, labelsQuery } from '../api'
import { useInboxFilters } from '../filters'
import { categories } from '../meta'
import { CountBadge, LabelDot } from './bits'
import { LabelManagerDialog } from './label-manager-dialog'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'

/** Columna de categorías (con contadores) y etiquetas. Solo en pantallas anchas. */
export function InboxSidebar({ className }: { className?: string }) {
  const { filters, setFilters } = useInboxFilters()
  const counts = useQuery(countsQuery({ channel: filters.channel, labelId: filters.labelId }))
  const labels = useQuery(labelsQuery)
  const [manageOpen, setManageOpen] = useState(false)

  return (
    <nav
      aria-label="Categorías de la bandeja"
      className={cn('flex flex-col gap-6 overflow-y-auto p-3', className)}
    >
      <section>
        <h2 className="text-muted-foreground px-2 pb-2 text-xs font-medium tracking-wide uppercase">
          Bandeja
        </h2>
        <ul className="space-y-0.5">
          {categories.map((category) => {
            const active = filters.status === category.value
            const value = counts.data?.[category.count]
            return (
              <li key={category.value}>
                <button
                  type="button"
                  aria-current={active ? 'true' : undefined}
                  onClick={() => setFilters({ status: category.value })}
                  className={cn(
                    'focus-visible:ring-ring/50 flex w-full items-center gap-2.5 rounded-md px-2 py-1.5 text-sm transition-colors outline-none focus-visible:ring-[3px]',
                    active ? 'bg-primary/10 text-primary font-medium' : 'text-foreground/80 hover:bg-muted',
                  )}
                >
                  <category.icon className="size-4 shrink-0" aria-hidden />
                  <span className="truncate">{category.label}</span>
                  {counts.isPending ? (
                    <Skeleton className="ml-auto h-4 w-6" />
                  ) : (
                    <CountBadge
                      value={value}
                      active={category.value === 'Unattended' && (value ?? 0) > 0}
                      className={cn(active && category.value !== 'Unattended' && 'text-primary')}
                    />
                  )}
                </button>
              </li>
            )
          })}
        </ul>
      </section>

      <section>
        <div className="flex items-center justify-between px-2 pb-2">
          <h2 className="text-muted-foreground text-xs font-medium tracking-wide uppercase">Etiquetas</h2>
          <Button
            variant="ghost"
            size="icon-xs"
            aria-label="Gestionar etiquetas"
            title="Gestionar etiquetas"
            onClick={() => setManageOpen(true)}
          >
            <Settings2 />
          </Button>
        </div>
        {labels.isPending ? (
          <div className="space-y-1.5 px-2">
            <Skeleton className="h-5 w-3/4" />
            <Skeleton className="h-5 w-1/2" />
          </div>
        ) : labels.data && labels.data.length > 0 ? (
          <ul className="space-y-0.5">
            {labels.data.map((label) => {
              const active = filters.labelId === label.id
              return (
                <li key={label.id}>
                  <button
                    type="button"
                    aria-pressed={active}
                    onClick={() => setFilters({ label: active ? undefined : label.id })}
                    className={cn(
                      'focus-visible:ring-ring/50 flex w-full items-center gap-2.5 rounded-md px-2 py-1.5 text-sm transition-colors outline-none focus-visible:ring-[3px]',
                      active ? 'bg-muted font-medium' : 'text-foreground/80 hover:bg-muted',
                    )}
                  >
                    <LabelDot color={label.color} className="mx-1" />
                    <span className="truncate">{label.name}</span>
                    <CountBadge value={label.conversationCount} />
                  </button>
                </li>
              )
            })}
          </ul>
        ) : (
          <button
            type="button"
            onClick={() => setManageOpen(true)}
            className="text-muted-foreground hover:text-foreground hover:bg-muted flex w-full items-center gap-2 rounded-md px-2 py-1.5 text-sm transition-colors"
          >
            <Plus className="size-4" aria-hidden /> Crear etiqueta
          </button>
        )}
      </section>

      <LabelManagerDialog open={manageOpen} onOpenChange={setManageOpen} />
    </nav>
  )
}
