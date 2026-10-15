import type { LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'

interface SectionHeaderProps {
  id: string
  icon: LucideIcon
  title: string
  description?: ReactNode
  actions?: ReactNode
}

/** Encabezado de una sección dentro de una pestaña del vendedor. */
export function SectionHeader({ id, icon: Icon, title, description, actions }: SectionHeaderProps) {
  return (
    <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
      <div className="flex min-w-0 gap-3">
        <div className="bg-brand-accent/10 text-brand-accent flex size-10 shrink-0 items-center justify-center rounded-lg">
          <Icon className="size-5" aria-hidden />
        </div>
        <div className="min-w-0 space-y-0.5">
          <h2 id={id} className="text-lg font-semibold tracking-tight">
            {title}
          </h2>
          {description && <p className="text-muted-foreground text-sm">{description}</p>}
        </div>
      </div>
      {actions && <div className="flex shrink-0 flex-wrap items-center gap-2">{actions}</div>}
    </div>
  )
}
