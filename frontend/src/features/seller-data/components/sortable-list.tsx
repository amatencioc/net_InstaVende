import {
  closestCenter,
  DndContext,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
  type Announcements,
  type DragEndEvent,
  type UniqueIdentifier,
} from '@dnd-kit/core'
import {
  arrayMove,
  SortableContext,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { GripVertical } from 'lucide-react'
import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

interface SortableListProps<T extends { id: number }> {
  items: T[]
  /** Nombre legible de cada elemento (lectores de pantalla y etiqueta del asa). */
  getLabel: (item: T) => string
  /** Recibe los ids en el nuevo orden. */
  onReorder: (ids: number[]) => void
  renderItem: (item: T, handle: ReactNode) => ReactNode
  /** Etiqueta accesible de la lista. */
  label: string
  disabled?: boolean
  className?: string
}

/**
 * Lista vertical reordenable con arrastrar y soltar (ratón, táctil y teclado).
 * Cada elemento recibe su asa de arrastre para colocarla donde convenga.
 */
export function SortableList<T extends { id: number }>({
  items,
  getLabel,
  onReorder,
  renderItem,
  label,
  disabled,
  className,
}: SortableListProps<T>) {
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  )

  const nameOf = (id: UniqueIdentifier) => {
    const item = items.find((i) => i.id === id)
    return item ? `"${getLabel(item)}"` : 'el elemento'
  }
  const positionOf = (id: UniqueIdentifier) => items.findIndex((i) => i.id === id) + 1

  const announcements: Announcements = {
    onDragStart: ({ active }) => `Moviendo ${nameOf(active.id)}, posición ${positionOf(active.id)} de ${items.length}.`,
    onDragOver: ({ active, over }) =>
      over ? `${nameOf(active.id)} sobre la posición ${positionOf(over.id)} de ${items.length}.` : undefined,
    onDragEnd: ({ active, over }) =>
      over ? `${nameOf(active.id)} quedó en la posición ${positionOf(over.id)} de ${items.length}.` : undefined,
    onDragCancel: ({ active }) => `Se canceló el movimiento de ${nameOf(active.id)}.`,
  }

  const handleDragEnd = ({ active, over }: DragEndEvent) => {
    if (!over || active.id === over.id) return
    const from = items.findIndex((i) => i.id === active.id)
    const to = items.findIndex((i) => i.id === over.id)
    if (from < 0 || to < 0) return
    onReorder(arrayMove(items, from, to).map((i) => i.id))
  }

  return (
    <DndContext
      sensors={sensors}
      collisionDetection={closestCenter}
      onDragEnd={handleDragEnd}
      accessibility={{
        announcements,
        screenReaderInstructions: {
          draggable:
            'Para reordenar, presiona espacio o Enter para tomar el elemento, usa las flechas para moverlo y vuelve a presionar espacio o Enter para soltarlo. Escape cancela.',
        },
      }}
    >
      <SortableContext items={items.map((i) => i.id)} strategy={verticalListSortingStrategy} disabled={disabled}>
        <ul aria-label={label} className={cn('flex flex-col gap-3', className)}>
          {items.map((item) => (
            <SortableRow key={item.id} id={item.id} label={getLabel(item)} disabled={disabled}>
              {(handle) => renderItem(item, handle)}
            </SortableRow>
          ))}
        </ul>
      </SortableContext>
    </DndContext>
  )
}

function SortableRow({
  id,
  label,
  disabled,
  children,
}: {
  id: number
  label: string
  disabled?: boolean
  children: (handle: ReactNode) => ReactNode
}) {
  const { attributes, listeners, setNodeRef, setActivatorNodeRef, transform, transition, isDragging } = useSortable({
    id,
    disabled,
  })

  const handle = (
    <button
      type="button"
      ref={setActivatorNodeRef}
      {...attributes}
      {...listeners}
      aria-label={`Reordenar ${label}`}
      disabled={disabled}
      className="text-muted-foreground hover:text-foreground hover:bg-accent focus-visible:ring-ring/50 inline-flex size-8 shrink-0 cursor-grab touch-none items-center justify-center rounded-md outline-none focus-visible:ring-[3px] active:cursor-grabbing disabled:cursor-not-allowed disabled:opacity-40"
    >
      <GripVertical className="size-4" aria-hidden />
    </button>
  )

  return (
    <li
      ref={setNodeRef}
      style={{ transform: CSS.Translate.toString(transform), transition }}
      className={cn('relative', isDragging && 'z-10 opacity-90 [&>*]:shadow-lg')}
    >
      {children(handle)}
    </li>
  )
}
