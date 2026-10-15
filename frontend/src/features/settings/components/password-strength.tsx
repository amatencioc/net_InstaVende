import { Check, X } from 'lucide-react'
import { cn } from '@/lib/utils'

interface Requirement {
  label: string
  test: (password: string) => boolean
  /** Obligatoria para el servidor; las demás solo suben la fortaleza. */
  required?: boolean
}

const requirements: Requirement[] = [
  { label: 'Al menos 8 caracteres', test: (p) => p.length >= 8, required: true },
  { label: 'Al menos un número', test: (p) => /\d/.test(p), required: true },
  { label: 'Mayúsculas y minúsculas', test: (p) => /[a-z]/.test(p) && /[A-Z]/.test(p) },
  { label: 'Un símbolo (!@#$…)', test: (p) => /[^A-Za-z0-9]/.test(p) },
]

const levels = [
  { label: 'Muy débil', className: 'bg-destructive', text: 'text-destructive' },
  { label: 'Débil', className: 'bg-destructive', text: 'text-destructive' },
  { label: 'Aceptable', className: 'bg-warning', text: 'text-warning' },
  { label: 'Buena', className: 'bg-info', text: 'text-info' },
  { label: 'Excelente', className: 'bg-success', text: 'text-success' },
] as const

/**
 * Puntuación 0–4: sin cumplir el mínimo del servidor nunca pasa de "Débil";
 * cumpliéndolo parte de "Aceptable" y sube con cada recomendación y con 12+ caracteres.
 */
function score(password: string): number {
  if (!password) return 0
  const meetsMinimum = requirements.filter((r) => r.required).every((r) => r.test(password))
  if (!meetsMinimum) return 1
  const passed = requirements.filter((r) => r.test(password)).length
  const bonus = password.length >= 12 ? 1 : 0
  return Math.min(4, passed + bonus)
}

/** Medidor de fortaleza y lista de requisitos de la contraseña. */
export function PasswordStrength({ password }: { password: string }) {
  const value = score(password)
  const level = levels[value] ?? levels[0]

  return (
    <div className="space-y-2.5">
      <div className="flex items-center gap-3">
        <div className="grid flex-1 grid-cols-4 gap-1.5" aria-hidden>
          {[1, 2, 3, 4].map((i) => (
            <span
              key={i}
              className={cn('h-1.5 rounded-full transition-colors', password && value >= i ? level.className : 'bg-muted')}
            />
          ))}
        </div>
        <span className={cn('w-20 text-right text-xs font-medium', password ? level.text : 'text-muted-foreground')} aria-live="polite">
          {password ? level.label : 'Sin escribir'}
        </span>
      </div>
      <ul className="grid gap-1 text-xs sm:grid-cols-2" aria-label="Requisitos de la contraseña">
        {requirements.map((r) => {
          const ok = r.test(password)
          return (
            <li key={r.label} className={cn('flex items-center gap-1.5', ok ? 'text-success' : 'text-muted-foreground')}>
              {ok ? <Check className="size-3.5" aria-hidden /> : <X className="size-3.5" aria-hidden />}
              <span>
                {r.label}
                {!r.required && <span className="text-muted-foreground"> (recomendado)</span>}
              </span>
              <span className="sr-only">{ok ? 'cumplido' : 'pendiente'}</span>
            </li>
          )
        })}
      </ul>
    </div>
  )
}
