/**
 * Colores de los sub-estados. La API devuelve la clase semántica de OrderWorkflow.BadgeCss
 * (`st-purple`, `st-blue`…); aquí solo se traduce a utilidades de Tailwind con soporte de modo oscuro.
 */
interface StatusTone {
  /** Fondo + texto para badges. */
  badge: string
  /** Punto/indicador sólido. */
  dot: string
  /** Borde superior de la columna del tablero. */
  accent: string
}

const tones: Record<string, StatusTone> = {
  'st-purple': {
    badge: 'bg-brand-accent/10 text-brand-accent',
    dot: 'bg-brand-accent',
    accent: 'border-t-brand-accent',
  },
  'st-blue': { badge: 'bg-info/10 text-info', dot: 'bg-info', accent: 'border-t-info' },
  'st-orange': {
    badge: 'bg-orange-500/10 text-orange-700 dark:text-orange-300',
    dot: 'bg-orange-500',
    accent: 'border-t-orange-500',
  },
  'st-sky': {
    badge: 'bg-sky-500/10 text-sky-700 dark:text-sky-300',
    dot: 'bg-sky-500',
    accent: 'border-t-sky-500',
  },
  'st-green': { badge: 'bg-success/10 text-success', dot: 'bg-success', accent: 'border-t-success' },
  'st-red': { badge: 'bg-destructive/10 text-destructive', dot: 'bg-destructive', accent: 'border-t-destructive' },
}

const fallback: StatusTone = { badge: 'bg-muted text-muted-foreground', dot: 'bg-muted-foreground', accent: 'border-t-border' }

export function statusTone(css: string | null | undefined): StatusTone {
  return (css && tones[css]) || fallback
}
