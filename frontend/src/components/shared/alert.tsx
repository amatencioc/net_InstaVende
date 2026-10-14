import { AlertCircle, CheckCircle2, Info, TriangleAlert } from 'lucide-react'
import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

const tones = {
  info: { icon: Info, className: 'border-info/30 bg-info/5 text-info' },
  success: { icon: CheckCircle2, className: 'border-success/30 bg-success/5 text-success' },
  warning: { icon: TriangleAlert, className: 'border-warning/40 bg-warning/10 text-warning' },
  destructive: { icon: AlertCircle, className: 'border-destructive/30 bg-destructive/5 text-destructive' },
}

/** Mensaje en línea (errores de formulario, avisos). */
export function Alert({
  tone = 'info',
  title,
  children,
  className,
}: {
  tone?: keyof typeof tones
  title?: string
  children?: ReactNode
  className?: string
}) {
  const { icon: Icon, className: toneClass } = tones[tone]
  return (
    <div
      role={tone === 'destructive' ? 'alert' : 'status'}
      className={cn('flex gap-3 rounded-lg border p-3 text-sm', toneClass, className)}
    >
      <Icon className="mt-0.5 size-4 shrink-0" aria-hidden />
      <div className="text-foreground space-y-0.5">
        {title && <p className="font-medium">{title}</p>}
        {children && <div className="text-foreground/80">{children}</div>}
      </div>
    </div>
  )
}
