import { X } from 'lucide-react'
import { useState, type ClipboardEvent, type KeyboardEvent } from 'react'
import { cn } from '@/lib/utils'

interface TagInputProps {
  value: string[]
  onChange: (value: string[]) => void
  id?: string
  placeholder?: string
  disabled?: boolean
  /** Cantidad máxima de etiquetas. */
  maxItems?: number
  /** Longitud mínima/máxima de cada etiqueta. */
  minLength?: number
  maxLength?: number
  /** Nombre de cada elemento para los lectores de pantalla ("palabra", "frase"…). */
  itemLabel?: string
  className?: string
  'aria-invalid'?: boolean
  'aria-describedby'?: string
}

const SEPARATORS = /[\n,;]/

/**
 * Campo de etiquetas (chips). Enter o coma añaden; Retroceso con el campo vacío quita la última;
 * pegar texto separado por comas o líneas añade varias a la vez.
 */
export function TagInput({
  value,
  onChange,
  id,
  placeholder,
  disabled,
  maxItems = 50,
  minLength = 1,
  maxLength = 200,
  itemLabel = 'elemento',
  className,
  ...aria
}: TagInputProps) {
  const [draft, setDraft] = useState('')
  const [hint, setHint] = useState<string | null>(null)
  const full = value.length >= maxItems

  function add(raw: string[]) {
    const next = [...value]
    let problem: string | null = null
    for (const item of raw.map((r) => r.trim()).filter(Boolean)) {
      if (item.length < minLength) problem = `Cada ${itemLabel} debe tener al menos ${minLength} caracteres.`
      else if (item.length > maxLength) problem = `Cada ${itemLabel} admite como máximo ${maxLength} caracteres.`
      else if (next.length >= maxItems) problem = `Máximo ${maxItems} elementos.`
      else if (!next.some((v) => v.toLowerCase() === item.toLowerCase())) next.push(item)
    }
    setHint(problem)
    if (next.length !== value.length) onChange(next)
    return problem === null
  }

  function commitDraft() {
    if (!draft.trim()) return
    if (add([draft])) setDraft('')
  }

  function onKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'Enter' || e.key === ',') {
      e.preventDefault()
      commitDraft()
    } else if (e.key === 'Backspace' && draft === '' && value.length > 0) {
      onChange(value.slice(0, -1))
    }
  }

  function onPaste(e: ClipboardEvent<HTMLInputElement>) {
    const text = e.clipboardData.getData('text')
    if (!SEPARATORS.test(text)) return
    e.preventDefault()
    add(text.split(SEPARATORS))
  }

  return (
    <div className="space-y-1.5">
      <div
        className={cn(
          'border-input dark:bg-input/30 flex min-h-9 w-full flex-wrap items-center gap-1.5 rounded-md border bg-transparent px-2 py-1.5 shadow-xs transition-[color,box-shadow]',
          'focus-within:border-ring focus-within:ring-ring/50 focus-within:ring-[3px]',
          aria['aria-invalid'] && 'border-destructive ring-destructive/20',
          disabled && 'cursor-not-allowed opacity-50',
          className,
        )}
      >
        <ul className="contents" aria-label="Elementos agregados">
          {value.map((tag) => (
            <li
              key={tag}
              className="bg-secondary text-secondary-foreground inline-flex max-w-full items-center gap-1 rounded-full py-0.5 pr-1 pl-2.5 text-xs font-medium"
            >
              <span className="truncate">{tag}</span>
              <button
                type="button"
                disabled={disabled}
                onClick={() => onChange(value.filter((v) => v !== tag))}
                className="hover:bg-foreground/10 focus-visible:ring-ring/50 rounded-full p-0.5 outline-none focus-visible:ring-2"
                aria-label={`Quitar ${itemLabel} "${tag}"`}
              >
                <X className="size-3" aria-hidden />
              </button>
            </li>
          ))}
        </ul>
        <input
          id={id}
          value={draft}
          disabled={disabled || full}
          onChange={(e) => {
            setDraft(e.target.value)
            if (hint) setHint(null)
          }}
          onKeyDown={onKeyDown}
          onPaste={onPaste}
          onBlur={commitDraft}
          placeholder={full ? `Límite de ${maxItems} alcanzado` : placeholder}
          maxLength={maxLength}
          className="placeholder:text-muted-foreground min-w-[10rem] flex-1 bg-transparent px-1 text-base outline-none disabled:cursor-not-allowed md:text-sm"
          {...aria}
        />
      </div>
      {hint ? (
        <p className="text-warning text-xs" role="status">
          {hint}
        </p>
      ) : (
        <p className="text-muted-foreground text-xs">Pulsa Enter o coma para añadir. También puedes pegar una lista.</p>
      )}
    </div>
  )
}
