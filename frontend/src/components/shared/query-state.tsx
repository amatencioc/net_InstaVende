import { AlertTriangle, RotateCcw } from 'lucide-react'
import { Button } from '@/components/ui/button'

/** Error de carga con opción de reintentar (para bloques que usan useQuery). */
export function QueryError({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  const message = error instanceof Error ? error.message : 'No se pudo cargar la información.'
  return (
    <div
      role="alert"
      className="border-destructive/30 bg-destructive/5 flex flex-col items-center gap-3 rounded-xl border px-6 py-10 text-center"
    >
      <AlertTriangle className="text-destructive size-6" aria-hidden />
      <p className="text-sm">{message}</p>
      {onRetry && (
        <Button variant="outline" size="sm" onClick={onRetry}>
          <RotateCcw /> Reintentar
        </Button>
      )}
    </div>
  )
}
