import { Check, Copy } from 'lucide-react'
import { useEffect, useId, useRef, useState } from 'react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'

/** Campo de solo lectura con botón para copiar su valor al portapapeles. */
export function CopyField({ value, label }: { value: string; label: string }) {
  const id = useId()
  const inputRef = useRef<HTMLInputElement>(null)
  const [copied, setCopied] = useState(false)

  useEffect(() => {
    if (!copied) return
    const timer = setTimeout(() => setCopied(false), 2000)
    return () => clearTimeout(timer)
  }, [copied])

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(value)
      setCopied(true)
      toast.success('Enlace copiado.')
    } catch {
      // Sin permiso de portapapeles: se selecciona el texto para copiarlo a mano
      inputRef.current?.select()
      toast.info('Selecciona el enlace y cópialo con Ctrl+C.')
    }
  }

  return (
    <div className="space-y-1.5">
      <label htmlFor={id} className="text-sm font-medium">
        {label}
      </label>
      <div className="flex gap-2">
        <Input
          id={id}
          ref={inputRef}
          readOnly
          value={value}
          onFocus={(e) => e.currentTarget.select()}
          className="font-mono text-xs"
        />
        <Button type="button" onClick={copy} className="shrink-0" aria-label={copied ? 'Enlace copiado' : 'Copiar enlace'}>
          {copied ? <Check /> : <Copy />}
          <span className="hidden sm:inline">{copied ? 'Copiado' : 'Copiar'}</span>
        </Button>
      </div>
    </div>
  )
}
