import { useId, useState } from 'react'
import { Checkbox } from '@/components/ui/checkbox'

/**
 * Contenido del diálogo de cancelación/rechazo con la opción de avisar al cliente.
 * Se renderiza dentro de la descripción de useConfirm (un <p>), por eso solo usa elementos en línea.
 */
export function CancelDescription({
  text,
  canNotify,
  defaultNotify,
  onNotifyChange,
}: {
  text: string
  canNotify: boolean
  defaultNotify: boolean
  onNotifyChange: (value: boolean) => void
}) {
  const id = useId()
  const [checked, setChecked] = useState(defaultNotify)
  return (
    <>
      <span className="block">{text}</span>
      {canNotify ? (
        <span className="text-foreground mt-4 flex items-center gap-2">
          <Checkbox
            id={id}
            checked={checked}
            onCheckedChange={(value) => {
              const next = value === true
              setChecked(next)
              onNotifyChange(next)
            }}
          />
          <label htmlFor={id} className="cursor-pointer text-sm font-medium">
            Avisar al cliente por su canal
          </label>
        </span>
      ) : (
        <span className="mt-3 block text-xs">Este pedido no tiene una conversación asociada para avisar al cliente.</span>
      )}
    </>
  )
}
