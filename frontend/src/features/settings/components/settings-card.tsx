import { Lock } from 'lucide-react'
import type { ReactNode } from 'react'
import { EmptyState } from '@/components/shared/empty-state'
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'

interface SettingsCardProps {
  title: string
  description?: ReactNode
  /** Acción a la derecha de la cabecera. */
  action?: ReactNode
  children: ReactNode
  /** Pie con botones (p.ej. Guardar). */
  footer?: ReactNode
  className?: string
}

/** Bloque estándar de una pestaña de configuración. */
export function SettingsCard({ title, description, action, children, footer, className }: SettingsCardProps) {
  return (
    <Card className={cn('gap-5', className)}>
      <CardHeader className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div className="min-w-0 space-y-1.5">
          <CardTitle className="text-base">{title}</CardTitle>
          {description && <CardDescription>{description}</CardDescription>}
        </div>
        {action && <div className="shrink-0">{action}</div>}
      </CardHeader>
      <CardContent>{children}</CardContent>
      {footer && (
        <CardFooter className="bg-muted/40 -mb-6 flex-wrap justify-end gap-2 rounded-b-xl border-t py-3">{footer}</CardFooter>
      )}
    </Card>
  )
}

/** Esqueleto de una tarjeta de formulario mientras carga. */
export function SettingsCardSkeleton({ rows = 3 }: { rows?: number }) {
  return (
    <Card className="gap-5" aria-busy="true" aria-label="Cargando">
      <CardHeader className="space-y-2">
        <Skeleton className="h-5 w-40" />
        <Skeleton className="h-4 w-72 max-w-full" />
      </CardHeader>
      <CardContent className="space-y-4">
        {Array.from({ length: rows }, (_, i) => (
          <div key={i} className="space-y-2">
            <Skeleton className="h-4 w-24" />
            <Skeleton className="h-9 w-full" />
          </div>
        ))}
      </CardContent>
    </Card>
  )
}

/** Aviso para miembros que abren una sección reservada a administradores. */
export function AdminOnlyNotice({ section }: { section: string }) {
  return (
    <EmptyState
      icon={Lock}
      title="Solo para administradores"
      description={`La sección ${section} solo pueden modificarla los administradores del negocio. Pide a un administrador que haga el cambio o que te asigne ese rol.`}
    />
  )
}
