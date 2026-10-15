import { Link } from '@tanstack/react-router'
import { ArrowRight, Check, Rocket } from 'lucide-react'
import { Card } from '@/components/ui/card'
import { Progress } from '@/components/ui/progress'
import { cn } from '@/lib/utils'
import type { FileRoutesByTo } from '@/routeTree.gen'
import type { DashboardData } from '../../api'

/** Pantalla a la que lleva cada paso del checklist (claves de DashboardSetupStepDto.key). */
const stepLinks: Record<string, keyof FileRoutesByTo> = {
  channel: '/channels',
  products: '/products',
  payments: '/seller/payments',
  vendor: '/seller/personality',
  'bot-test': '/seller/test',
}

/** Banner "Primeros pasos": progreso de la configuración inicial. Se oculta al completarla. */
export function SetupChecklist({ setup }: { setup: DashboardData['setup'] }) {
  if (setup.percent >= 100) return null
  const nextKey = setup.steps.find((s) => !s.done)?.key

  return (
    <Card className="from-primary/8 via-card to-card border-primary/25 gap-0 overflow-hidden bg-linear-to-br py-0">
      <div className="flex flex-col gap-4 p-5 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-start gap-3">
          <div className="bg-primary/12 text-primary flex size-10 shrink-0 items-center justify-center rounded-lg">
            <Rocket className="size-5" aria-hidden />
          </div>
          <div className="space-y-0.5">
            <h2 className="text-lg font-semibold">Primeros pasos</h2>
            <p className="text-muted-foreground text-sm">
              Completa la configuración para que tu vendedor IA empiece a atender y vender.
            </p>
          </div>
        </div>
        <div className="w-full shrink-0 space-y-1.5 sm:w-64">
          <div className="flex items-baseline justify-between text-sm">
            <span className="text-muted-foreground">
              {setup.completedSteps} de {setup.totalSteps} pasos
            </span>
            <span className="font-semibold tabular-nums">{setup.percent}%</span>
          </div>
          <Progress
            value={setup.percent}
            aria-label={`Configuración inicial completada al ${setup.percent}%`}
          />
        </div>
      </div>
      <ol className="grid gap-2 border-t p-3 sm:grid-cols-2 lg:grid-cols-5">
        {setup.steps.map((step, index) => {
          const isNext = step.key === nextKey
          const to = stepLinks[step.key] ?? '/dashboard'
          return (
            <li key={step.key} className="flex">
              <Link
                to={to}
                className={cn(
                  'group flex w-full flex-col gap-2 rounded-lg border p-3 text-left transition-colors',
                  'hover:bg-muted/60 focus-visible:ring-ring/50 outline-none focus-visible:ring-[3px]',
                  isNext && 'border-primary/40 bg-primary/5',
                  step.done && 'border-transparent bg-transparent',
                )}
                aria-label={`${step.title}${step.done ? ' (completado)' : ''}`}
              >
                <div className="flex items-center gap-2">
                  <span
                    className={cn(
                      'flex size-6 shrink-0 items-center justify-center rounded-full text-xs font-semibold',
                      step.done ? 'bg-success text-primary-foreground' : 'bg-muted text-muted-foreground',
                      isNext && 'bg-primary text-primary-foreground',
                    )}
                    aria-hidden
                  >
                    {step.done ? <Check className="size-3.5" /> : index + 1}
                  </span>
                  <span
                    className={cn(
                      'text-sm leading-tight font-medium',
                      step.done && 'text-muted-foreground line-through decoration-1',
                    )}
                  >
                    {step.title}
                  </span>
                </div>
                {!step.done && (
                  <p className="text-muted-foreground line-clamp-2 text-xs">{step.description}</p>
                )}
                {isNext && (
                  <span className="text-primary mt-auto inline-flex items-center gap-1 text-xs font-medium">
                    Empezar{' '}
                    <ArrowRight
                      className="size-3 transition-transform group-hover:translate-x-0.5"
                      aria-hidden
                    />
                  </span>
                )}
              </Link>
            </li>
          )
        })}
      </ol>
    </Card>
  )
}
