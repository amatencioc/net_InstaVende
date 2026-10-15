import { useQuery } from '@tanstack/react-query'
import { Check, Sparkles } from 'lucide-react'
import { useState } from 'react'
import { QueryError } from '@/components/shared/query-state'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { Skeleton } from '@/components/ui/skeleton'
import { reminderTemplateQueries, type CustomerSegment } from '../api'
import { reminderOrderTitle, segmentMeta } from '../constants'

interface SuggestionsDialogProps {
  segment: CustomerSegment
  /** Recordatorio que se está editando: sus sugerencias aparecen primero. */
  order: number
  current: string
  onPick: (message: string) => void
  disabled?: boolean
}

/** Botón "Plantillas" que abre los mensajes sugeridos del segmento. */
export function SuggestionsDialog({ segment, order, current, onPick, disabled }: SuggestionsDialogProps) {
  const [open, setOpen] = useState(false)
  const suggestions = useQuery({ ...reminderTemplateQueries.suggestions(segment), enabled: open })
  const sorted = [...(suggestions.data ?? [])].sort(
    (a, b) => Number(b.order === order) - Number(a.order === order),
  )

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button type="button" variant="outline" size="sm" disabled={disabled}>
          <Sparkles className="text-brand-accent" aria-hidden /> Plantillas
        </Button>
      </DialogTrigger>
      <DialogContent className="max-h-[calc(100dvh-2rem)] overflow-y-auto sm:max-w-xl">
        <DialogHeader>
          <DialogTitle>Plantillas para clientes {segmentMeta[segment].label.toLowerCase()}s</DialogTitle>
          <DialogDescription>
            Elige un mensaje probado para el {reminderOrderTitle[order]?.toLowerCase()}. Podrás editarlo
            después.
          </DialogDescription>
        </DialogHeader>

        {suggestions.isError ? (
          <QueryError error={suggestions.error} onRetry={() => void suggestions.refetch()} />
        ) : suggestions.isPending ? (
          <div className="space-y-3">
            {Array.from({ length: 4 }, (_, i) => (
              <Skeleton key={i} className="h-20 w-full rounded-lg" />
            ))}
          </div>
        ) : (
          <ul className="space-y-2">
            {sorted.map((s) => {
              const selected = s.message === current.trim()
              return (
                <li key={s.id}>
                  <button
                    type="button"
                    onClick={() => {
                      onPick(s.message)
                      setOpen(false)
                    }}
                    className="hover:border-primary/50 hover:bg-primary/5 focus-visible:ring-ring/50 aria-pressed:border-primary aria-pressed:bg-primary/5 w-full rounded-lg border p-3 text-left transition-colors outline-none focus-visible:ring-[3px]"
                    aria-pressed={selected}
                  >
                    <span className="mb-1 flex items-center justify-between gap-2">
                      <span className="text-sm font-medium">{s.title}</span>
                      <span className="flex items-center gap-1.5">
                        {s.order === order && (
                          <Badge variant="secondary" className="font-normal">
                            Recomendado
                          </Badge>
                        )}
                        {selected && <Check className="text-primary size-4" aria-label="En uso" />}
                      </span>
                    </span>
                    <span className="text-muted-foreground block text-sm">{s.message}</span>
                  </button>
                </li>
              )
            })}
          </ul>
        )}
      </DialogContent>
    </Dialog>
  )
}
