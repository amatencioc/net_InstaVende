import { useState, type ComponentProps, type ReactNode } from 'react'
import { Input } from '@/components/ui/input'
import { cn } from '@/lib/utils'

type NumberInputProps = Omit<ComponentProps<'input'>, 'value' | 'onChange' | 'type'> & {
  value: number | null | undefined
  /** null si el campo queda vacío y `allowEmpty`; NaN si es obligatorio (Zod lo marca como inválido). */
  onChange: (value: number | null) => void
  allowEmpty?: boolean
  allowNegative?: boolean
  integer?: boolean
  /** Prefijo visual (p.ej. símbolo de moneda). */
  prefix?: ReactNode
}

const toText = (value: number | null | undefined) =>
  value == null || Number.isNaN(value) ? '' : String(value)

/**
 * Campo numérico controlado que conserva lo que escribe el usuario ("12.", "-", "0,5")
 * mientras emite números al formulario. Acepta coma o punto decimal.
 */
export function NumberInput({
  value,
  onChange,
  allowEmpty,
  allowNegative,
  integer,
  prefix,
  className,
  onBlur,
  ...props
}: NumberInputProps) {
  const [text, setText] = useState(() => toText(value))
  const [lastValue, setLastValue] = useState(value)

  // Sincroniza cambios externos (reset del formulario) sin pisar lo que se está escribiendo
  if (!Object.is(value, lastValue)) {
    setLastValue(value)
    const parsed = text === '' ? null : Number(text.replace(',', '.'))
    if (!Object.is(parsed, value ?? null)) setText(toText(value))
  }

  const pattern = integer
    ? allowNegative
      ? /^-?\d*$/
      : /^\d*$/
    : allowNegative
      ? /^-?\d*[.,]?\d{0,2}$/
      : /^\d*[.,]?\d{0,2}$/

  const input = (
    <Input
      {...props}
      type="text"
      inputMode={integer ? 'numeric' : 'decimal'}
      autoComplete="off"
      value={text}
      className={cn('tabular-nums', prefix != null && 'pl-10', className)}
      onChange={(e) => {
        const next = e.target.value.trim()
        if (!pattern.test(next)) return
        setText(next)
        const normalized = next.replace(',', '.')
        if (normalized === '' || normalized === '-' || normalized === '.' || normalized === '-.') {
          onChange(allowEmpty && normalized === '' ? null : Number.NaN)
        } else onChange(Number(normalized))
      }}
      onBlur={(e) => {
        // Normaliza "12." → "12"
        if (text !== '' && !Number.isNaN(Number(text.replace(',', '.')))) setText(toText(Number(text.replace(',', '.'))))
        onBlur?.(e)
      }}
    />
  )

  if (prefix == null) return input
  return (
    <div className="relative">
      <span className="text-muted-foreground pointer-events-none absolute inset-y-0 left-0 flex w-10 items-center justify-center text-sm">
        {prefix}
      </span>
      {input}
    </div>
  )
}
