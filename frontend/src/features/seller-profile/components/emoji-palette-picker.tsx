import { Plus, X } from 'lucide-react'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { cn } from '@/lib/utils'

/** Emojis frecuentes en conversaciones de venta. */
const COMMON_EMOJIS = [
  '😊', '😄', '😉', '🥰', '😍', '🤩', '😎', '🤗',
  '🙌', '👏', '👍', '👌', '🙏', '💪', '👉', '🤝',
  '✨', '🔥', '💯', '⭐', '🎉', '🎁', '💥', '⚡',
  '❤️', '💚', '💜', '💛', '✅', '📦', '🚚', '🛒',
  '🛍️', '💳', '💰', '🏷️', '📍', '⏰', '📸', '📲',
]

const MAX_EMOJIS = 24

interface EmojiPalettePickerProps {
  value: string[]
  onChange: (value: string[]) => void
  disabled?: boolean
  id?: string
}

/** Rejilla de emojis comunes (alternables) + campo para añadir cualquier otro emoji. */
export function EmojiPalettePicker({ value, onChange, disabled, id }: EmojiPalettePickerProps) {
  const [custom, setCustom] = useState('')
  const full = value.length >= MAX_EMOJIS
  const extra = value.filter((e) => !COMMON_EMOJIS.includes(e))

  function toggle(emoji: string) {
    if (value.includes(emoji)) onChange(value.filter((e) => e !== emoji))
    else if (!full) onChange([...value, emoji])
  }

  function addCustom() {
    // Se separan los emojis escritos juntos ("🍕🍔") respetando los compuestos (👍🏽, ❤️).
    const segmenter = new Intl.Segmenter('es', { granularity: 'grapheme' })
    const items = [...segmenter.segment(custom)]
      .map((s) => s.segment.trim())
      .filter((s) => s && !/^[\p{L}\p{N}\p{P}]+$/u.test(s))
    const next = [...value]
    for (const item of items) if (!next.includes(item) && next.length < MAX_EMOJIS) next.push(item)
    onChange(next)
    setCustom('')
  }

  return (
    <div className={cn('space-y-3', disabled && 'pointer-events-none opacity-50')} aria-disabled={disabled}>
      <div className="flex min-h-10 flex-wrap items-center gap-1.5 rounded-lg border border-dashed p-2" id={id}>
        {value.length === 0 ? (
          <span className="text-muted-foreground px-1 text-sm">
            Sin paleta: el vendedor usará emojis con moderación según el contexto.
          </span>
        ) : (
          value.map((emoji) => (
            <button
              key={emoji}
              type="button"
              onClick={() => toggle(emoji)}
              disabled={disabled}
              className="bg-secondary hover:bg-destructive/10 group focus-visible:ring-ring/50 inline-flex items-center gap-1 rounded-full py-0.5 pr-1.5 pl-2 text-base outline-none focus-visible:ring-2"
              aria-label={`Quitar ${emoji} de la paleta`}
            >
              {emoji}
              <X className="text-muted-foreground group-hover:text-destructive size-3" aria-hidden />
            </button>
          ))
        )}
        <span className="text-muted-foreground ml-auto pr-1 text-xs tabular-nums">
          {value.length}/{MAX_EMOJIS}
        </span>
      </div>

      <div
        role="group"
        aria-label="Emojis sugeridos"
        className="grid grid-cols-8 gap-1 sm:grid-cols-10 lg:grid-cols-[repeat(20,minmax(0,1fr))]"
      >
        {[...COMMON_EMOJIS, ...extra].map((emoji) => {
          const selected = value.includes(emoji)
          return (
            <button
              key={emoji}
              type="button"
              disabled={disabled || (!selected && full)}
              aria-pressed={selected}
              aria-label={selected ? `Quitar ${emoji}` : `Añadir ${emoji}`}
              onClick={() => toggle(emoji)}
              className={cn(
                'focus-visible:ring-ring/50 flex aspect-square items-center justify-center rounded-md text-lg transition-colors outline-none focus-visible:ring-2 disabled:opacity-40',
                selected ? 'bg-primary/15 ring-primary/50 ring-1' : 'hover:bg-muted',
              )}
            >
              {emoji}
            </button>
          )
        })}
      </div>

      <div className="flex gap-2">
        <Input
          value={custom}
          onChange={(e) => setCustom(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') {
              e.preventDefault()
              addCustom()
            }
          }}
          placeholder="Pega o escribe otro emoji…"
          aria-label="Añadir otro emoji"
          maxLength={32}
          disabled={disabled || full}
          className="max-w-60"
        />
        <Button type="button" variant="outline" onClick={addCustom} disabled={disabled || full || !custom.trim()}>
          <Plus /> Añadir
        </Button>
      </div>
    </div>
  )
}
