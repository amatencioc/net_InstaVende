import {
  closestCenter,
  DndContext,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
  type Announcements,
  type DragEndEvent,
} from '@dnd-kit/core'
import {
  arrayMove,
  rectSortingStrategy,
  SortableContext,
  sortableKeyboardCoordinates,
  useSortable,
} from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { GripVertical, ImagePlus, Loader2, Star, Trash2, UploadCloud } from 'lucide-react'
import { useDropzone, type FileRejection } from 'react-dropzone'
import { toast } from 'sonner'
import { useConfirm } from '@/components/shared/confirm-dialog'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import {
  useDeleteProductImage,
  useReorderProductImages,
  useUploadProductImages,
  type ProductImage,
} from '../api'
import {
  ACCEPTED_IMAGE_TYPES,
  imageSrc,
  MAX_IMAGE_BYTES,
  plural,
  revokePendingImages,
  toPendingImages,
  type PendingImage,
} from '../lib'
import { MAX_IMAGES } from '../schemas'

// ── Galería genérica (ordenable) ─────────────────────────────────────────────

interface GalleryItem {
  id: string
  src: string | undefined
  busy?: boolean
}

interface GalleryProps {
  items: GalleryItem[]
  onReorder?: (ids: string[]) => void
  onRemove?: (id: string) => void
  onMakeMain?: (id: string) => void
  onFiles: (files: File[]) => void
  uploading?: boolean
  disabled?: boolean
}

const announcements: Announcements = {
  onDragStart: ({ active }) => `Imagen ${position(active.data.current)} seleccionada.`,
  onDragOver: ({ over }) => (over ? `Sobre la posición ${position(over.data.current)}.` : undefined),
  onDragEnd: ({ over }) =>
    over ? `Imagen movida a la posición ${position(over.data.current)}.` : 'Imagen soltada fuera de la galería.',
  onDragCancel: () => 'Movimiento cancelado.',
}

function position(data: unknown): number {
  const index = (data as { sortable?: { index: number } } | undefined)?.sortable?.index ?? 0
  return index + 1
}

const rejectionMessage = (rejection: FileRejection): string => {
  const code = rejection.errors[0]?.code
  if (code === 'file-too-large') return `${rejection.file.name}: supera los 5 MB.`
  if (code === 'file-invalid-type') return `${rejection.file.name}: formato no permitido (usa JPG, PNG, WEBP o GIF).`
  if (code === 'too-many-files') return `Solo puedes tener ${MAX_IMAGES} imágenes por producto.`
  return `${rejection.file.name}: no se pudo añadir.`
}

function ImageGallery({ items, onReorder, onRemove, onMakeMain, onFiles, uploading, disabled }: GalleryProps) {
  const remaining = MAX_IMAGES - items.length
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  )

  const { getRootProps, getInputProps, isDragActive, open } = useDropzone({
    accept: ACCEPTED_IMAGE_TYPES,
    maxSize: MAX_IMAGE_BYTES,
    multiple: true,
    noClick: true,
    disabled: disabled || uploading || remaining <= 0,
    onDrop: (accepted, rejected) => {
      const tooMany = accepted.length > remaining
      const files = accepted.slice(0, Math.max(0, remaining))
      const messages = [...new Set(rejected.map(rejectionMessage))]
      if (tooMany) messages.push(`Solo puedes añadir ${plural(remaining, 'imagen más', 'imágenes más')}.`)
      messages.slice(0, 3).forEach((m) => toast.error(m))
      if (files.length > 0) onFiles(files)
    },
  })

  const onDragEnd = ({ active, over }: DragEndEvent) => {
    if (!over || active.id === over.id || !onReorder) return
    const ids = items.map((i) => i.id)
    const from = ids.indexOf(String(active.id))
    const to = ids.indexOf(String(over.id))
    if (from < 0 || to < 0) return
    onReorder(arrayMove(ids, from, to))
  }

  return (
    <div className="space-y-4">
      <div
        {...getRootProps({
          className: cn(
            'border-input hover:border-primary/50 hover:bg-primary/5 flex flex-col items-center justify-center gap-2 rounded-xl border-2 border-dashed px-4 py-8 text-center transition-colors',
            isDragActive && 'border-primary bg-primary/5',
            (remaining <= 0 || disabled) && 'pointer-events-none opacity-60',
          ),
        })}
      >
        <input {...getInputProps({ 'aria-label': 'Seleccionar imágenes del producto' })} />
        <div className="bg-primary/10 text-primary flex size-10 items-center justify-center rounded-full">
          {uploading ? <Loader2 className="size-5 animate-spin" aria-hidden /> : <UploadCloud className="size-5" aria-hidden />}
        </div>
        <div className="space-y-1">
          <p className="text-sm font-medium">
            {uploading
              ? 'Subiendo imágenes…'
              : remaining <= 0
                ? `Alcanzaste el máximo de ${MAX_IMAGES} imágenes`
                : isDragActive
                  ? 'Suelta las imágenes aquí'
                  : 'Arrastra y suelta tus imágenes'}
          </p>
          <p className="text-muted-foreground text-xs">
            JPG, PNG, WEBP o GIF · máx. 5 MB cada una · hasta {MAX_IMAGES} imágenes. También puedes pegarlas.
          </p>
        </div>
        <Button type="button" variant="outline" size="sm" onClick={open} disabled={remaining <= 0 || uploading || disabled}>
          <ImagePlus /> Seleccionar archivos
        </Button>
      </div>

      {items.length > 0 && (
        <DndContext
          sensors={sensors}
          collisionDetection={closestCenter}
          onDragEnd={onDragEnd}
          accessibility={{
            announcements,
            screenReaderInstructions: {
              draggable:
                'Para reordenar, pulsa espacio o Enter para levantar la imagen, usa las flechas para moverla y vuelve a pulsar espacio o Enter para soltarla. Escape cancela.',
            },
          }}
        >
          <SortableContext items={items.map((i) => i.id)} strategy={rectSortingStrategy}>
            <ul className="grid grid-cols-3 gap-3 sm:grid-cols-4 lg:grid-cols-6" aria-label="Imágenes del producto">
              {items.map((item, index) => (
                <SortableImage
                  key={item.id}
                  item={item}
                  index={index}
                  sortable={!!onReorder && !disabled}
                  onRemove={onRemove}
                  onMakeMain={onMakeMain}
                />
              ))}
            </ul>
          </SortableContext>
        </DndContext>
      )}
      {items.length > 1 && (
        <p className="text-muted-foreground text-xs">
          Arrastra para reordenar. La primera imagen es la principal: es la que verán tus clientes en el catálogo.
        </p>
      )}
    </div>
  )
}

function SortableImage({
  item,
  index,
  sortable,
  onRemove,
  onMakeMain,
}: {
  item: GalleryItem
  index: number
  sortable: boolean
  onRemove?: (id: string) => void
  onMakeMain?: (id: string) => void
}) {
  const { attributes, listeners, setNodeRef, setActivatorNodeRef, transform, transition, isDragging } = useSortable({
    id: item.id,
    disabled: !sortable,
  })
  const isMain = index === 0

  return (
    <li
      ref={setNodeRef}
      style={{ transform: CSS.Transform.toString(transform), transition }}
      className={cn(
        'group bg-muted relative aspect-square overflow-hidden rounded-lg border',
        isMain && 'ring-primary ring-2 ring-offset-2 ring-offset-background',
        isDragging && 'z-10 opacity-80 shadow-lg',
      )}
    >
      {item.src && <img src={item.src} alt={`Imagen ${index + 1} del producto`} className="size-full object-cover" />}
      {item.busy && (
        <div className="bg-background/70 absolute inset-0 flex items-center justify-center">
          <Loader2 className="size-5 animate-spin" aria-label="Procesando" />
        </div>
      )}
      {isMain && (
        <Badge className="absolute bottom-1.5 left-1.5 gap-1 shadow-sm">
          <Star className="fill-current" aria-hidden /> Principal
        </Badge>
      )}
      <div className="absolute inset-x-1.5 top-1.5 flex items-center justify-between gap-1 opacity-100 transition-opacity sm:opacity-0 sm:group-focus-within:opacity-100 sm:group-hover:opacity-100">
        {sortable ? (
          <button
            type="button"
            ref={setActivatorNodeRef}
            {...attributes}
            {...listeners}
            aria-label={`Mover imagen ${index + 1}`}
            className="bg-background/90 hover:bg-background focus-visible:ring-ring/50 flex size-7 cursor-grab items-center justify-center rounded-md shadow-sm outline-none focus-visible:ring-[3px] active:cursor-grabbing"
          >
            <GripVertical className="size-4" aria-hidden />
          </button>
        ) : (
          <span />
        )}
        <div className="flex gap-1">
          {!isMain && onMakeMain && (
            <button
              type="button"
              onClick={() => onMakeMain(item.id)}
              aria-label={`Marcar imagen ${index + 1} como principal`}
              title="Marcar como principal"
              className="bg-background/90 hover:bg-background focus-visible:ring-ring/50 flex size-7 items-center justify-center rounded-md shadow-sm outline-none focus-visible:ring-[3px]"
            >
              <Star className="size-4" aria-hidden />
            </button>
          )}
          {onRemove && (
            <button
              type="button"
              onClick={() => onRemove(item.id)}
              aria-label={`Eliminar imagen ${index + 1}`}
              title="Eliminar"
              className="bg-background/90 text-destructive hover:bg-background focus-visible:ring-ring/50 flex size-7 items-center justify-center rounded-md shadow-sm outline-none focus-visible:ring-[3px]"
            >
              <Trash2 className="size-4" aria-hidden />
            </button>
          )}
        </div>
      </div>
    </li>
  )
}

function moveToFront(ids: string[], id: string): string[] {
  return [id, ...ids.filter((x) => x !== id)]
}

// ── Producto existente: cambios inmediatos contra la API ─────────────────────

export function RemoteProductImages({
  productId,
  images,
  legacyImageUrl,
}: {
  productId: number
  images: ProductImage[]
  /** Productos anteriores a la galería: solo tienen imageUrl. */
  legacyImageUrl?: string | null
}) {
  const confirm = useConfirm()
  const upload = useUploadProductImages(productId)
  const remove = useDeleteProductImage(productId)
  const reorder = useReorderProductImages(productId)

  const items: GalleryItem[] =
    images.length === 0 && legacyImageUrl
      ? [{ id: 'legacy', src: imageSrc(legacyImageUrl) }]
      : images.map((img) => ({
          id: String(img.id),
          src: imageSrc(img.url),
          busy: remove.isPending && remove.variables === img.id,
        }))
  const isLegacy = images.length === 0 && !!legacyImageUrl

  const onRemove = async (id: string) => {
    const ok = await confirm({
      title: '¿Eliminar imagen?',
      description: 'La imagen se eliminará del producto de forma permanente.',
      confirmLabel: 'Eliminar imagen',
      tone: 'destructive',
    })
    if (ok) remove.mutate(Number(id), { onSuccess: () => toast.success('Imagen eliminada.') })
  }

  return (
    <ImageGallery
      items={items}
      uploading={upload.isPending}
      onFiles={(files) =>
        upload.mutate(files, {
          onSuccess: () => toast.success(plural(files.length, 'imagen subida', 'imágenes subidas')),
        })
      }
      onReorder={isLegacy ? undefined : (ids) => reorder.mutate(ids.map(Number))}
      onMakeMain={isLegacy ? undefined : (id) => reorder.mutate(moveToFront(items.map((i) => i.id), id).map(Number))}
      onRemove={isLegacy ? undefined : (id) => void onRemove(id)}
    />
  )
}

// ── Producto nuevo: imágenes locales que se suben tras crear ─────────────────

export function LocalProductImages({
  value,
  onChange,
  disabled,
}: {
  value: PendingImage[]
  onChange: (images: PendingImage[]) => void
  disabled?: boolean
}) {
  const items: GalleryItem[] = value.map((img) => ({ id: img.id, src: img.preview }))
  const byId = new Map(value.map((img) => [img.id, img]))
  const fromIds = (ids: string[]) => ids.map((id) => byId.get(id)).filter((x): x is PendingImage => !!x)

  return (
    <ImageGallery
      items={items}
      disabled={disabled}
      onFiles={(files) => onChange([...value, ...toPendingImages(files)])}
      onReorder={(ids) => onChange(fromIds(ids))}
      onMakeMain={(id) => onChange(fromIds(moveToFront(value.map((i) => i.id), id)))}
      onRemove={(id) => {
        const removed = byId.get(id)
        if (removed) revokePendingImages([removed])
        onChange(value.filter((img) => img.id !== id))
      }}
    />
  )
}
