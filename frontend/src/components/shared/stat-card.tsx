import type { LucideIcon } from 'lucide-react'
import { ArrowDownRight, ArrowUpRight } from 'lucide-react'
import type { ReactNode } from 'react'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'

interface StatCardProps {
  label: string
  value: ReactNode
  icon: LucideIcon
  /** Variación porcentual respecto al periodo anterior (positivo = mejora). */
  delta?: number | null
  hint?: ReactNode
  tone?: 'primary' | 'accent' | 'info' | 'warning'
  loading?: boolean
}

const tones = {
  primary: 'bg-primary/10 text-primary',
  accent: 'bg-brand-accent/10 text-brand-accent',
  info: 'bg-info/10 text-info',
  warning: 'bg-warning/15 text-warning',
}

/** Tarjeta de KPI para dashboards y métricas. */
export function StatCard({ label, value, icon: Icon, delta, hint, tone = 'primary', loading }: StatCardProps) {
  return (
    <Card className="gap-0 py-0">
      <CardContent className="flex items-start justify-between gap-4 p-5">
        <div className="min-w-0 space-y-1.5">
          <p className="text-muted-foreground text-sm font-medium">{label}</p>
          {loading ? (
            <Skeleton className="h-8 w-24" />
          ) : (
            <p className="font-display text-2xl font-semibold tabular-nums tracking-tight sm:text-3xl">{value}</p>
          )}
          {(delta != null || hint) && !loading && (
            <div className="flex items-center gap-2 text-xs">
              {delta != null && (
                <span
                  className={cn(
                    'inline-flex items-center gap-0.5 font-medium',
                    delta >= 0 ? 'text-success' : 'text-destructive',
                  )}
                >
                  {delta >= 0 ? <ArrowUpRight className="size-3.5" /> : <ArrowDownRight className="size-3.5" />}
                  {Math.abs(delta).toFixed(1)}%
                </span>
              )}
              {hint && <span className="text-muted-foreground truncate">{hint}</span>}
            </div>
          )}
        </div>
        <div className={cn('flex size-10 shrink-0 items-center justify-center rounded-lg', tones[tone])}>
          <Icon className="size-5" aria-hidden />
        </div>
      </CardContent>
    </Card>
  )
}
