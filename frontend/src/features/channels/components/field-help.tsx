import { CircleHelp } from 'lucide-react'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'

/** Icono de ayuda contextual accesible por teclado (tooltip con la explicación del campo). */
export function FieldHelp({ label, children }: { label: string; children: string }) {
  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <button
          type="button"
          className="text-muted-foreground hover:text-foreground focus-visible:ring-ring/50 inline-flex rounded-full outline-none focus-visible:ring-[3px]"
          aria-label={`Ayuda: ${label}`}
        >
          <CircleHelp className="size-3.5" aria-hidden />
        </button>
      </TooltipTrigger>
      <TooltipContent side="top" className="max-w-xs text-left">
        {children}
      </TooltipContent>
    </Tooltip>
  )
}
