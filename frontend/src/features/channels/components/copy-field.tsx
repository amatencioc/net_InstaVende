import { Check, Copy } from 'lucide-react'
import { useId, useState, type ReactNode } from 'react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

interface CopyFieldProps {
  label: ReactNode
  value: string
  /** Nombre accesible del botón de copiar (p.ej. "Copiar URL del webhook"). */
  copyLabel: string
  description?: ReactNode
  /** Acción adicional junto al botón de copiar. */
  action?: ReactNode
}

/** Campo de solo lectura con botón para copiar al portapapeles. */
export function CopyField({ label, value, copyLabel, description, action }: CopyFieldProps) {
  const id = useId()
  const [copied, setCopied] = useState(false)

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(value)
      setCopied(true)
      toast.success('Copiado al portapapeles')
      setTimeout(() => setCopied(false), 2_000)
    } catch {
      toast.error('No se pudo copiar. Selecciona el texto y cópialo manualmente.')
    }
  }

  return (
    <div className="space-y-2">
      <Label htmlFor={id}>{label}</Label>
      <div className="flex gap-2">
        <Input
          id={id}
          value={value}
          readOnly
          onFocus={(e) => e.currentTarget.select()}
          className="font-mono text-xs sm:text-sm"
          aria-describedby={description ? `${id}-desc` : undefined}
        />
        <Button
          type="button"
          variant="outline"
          size="icon"
          onClick={copy}
          aria-label={copyLabel}
          title={copyLabel}
        >
          {copied ? <Check className="text-success" /> : <Copy />}
        </Button>
        {action}
      </div>
      {description && (
        <p id={`${id}-desc`} className="text-muted-foreground text-xs">
          {description}
        </p>
      )}
    </div>
  )
}
