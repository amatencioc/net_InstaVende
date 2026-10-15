import { ImageUp, Loader2, Trash2 } from 'lucide-react'
import { useEffect, useState, type ReactNode } from 'react'
import { useDropzone, type FileRejection } from 'react-dropzone'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

const MAX_BYTES = 5 * 1024 * 1024
const ACCEPT = { 'image/jpeg': ['.jpg', '.jpeg'], 'image/png': ['.png'], 'image/webp': ['.webp'], 'image/gif': ['.gif'] }

interface ImageDropzoneProps {
  /** URL actual de la imagen (null si no hay). */
  imageUrl: string | null | undefined
  /** Contenido a mostrar cuando no hay imagen (iniciales, icono…). */
  fallback: ReactNode
  /** Nombre accesible de la imagen ("foto de perfil", "logo"). */
  label: string
  shape?: 'circle' | 'square'
  uploading?: boolean
  removing?: boolean
  onUpload: (file: File) => Promise<unknown>
  onRemove: () => void
}

function rejectionMessage(rejections: FileRejection[]): string {
  const code = rejections[0]?.errors[0]?.code
  if (code === 'file-too-large') return 'La imagen supera el tamaño máximo de 5 MB.'
  if (code === 'file-invalid-type') return 'Formato no permitido. Usa JPG, PNG, WEBP o GIF.'
  if (code === 'too-many-files') return 'Sube una sola imagen.'
  return 'No se pudo usar ese archivo.'
}

/** Subida de imagen con arrastrar y soltar, vista previa inmediata y opción de quitarla. */
export function ImageDropzone({
  imageUrl,
  fallback,
  label,
  shape = 'circle',
  uploading = false,
  removing = false,
  onUpload,
  onRemove,
}: ImageDropzoneProps) {
  const [preview, setPreview] = useState<string | null>(null)

  // Libera la URL temporal de la vista previa
  useEffect(() => {
    if (!preview) return
    return () => URL.revokeObjectURL(preview)
  }, [preview])

  const busy = uploading || removing
  const { getRootProps, getInputProps, isDragActive, open } = useDropzone({
    accept: ACCEPT,
    maxSize: MAX_BYTES,
    multiple: false,
    noClick: true,
    noKeyboard: true,
    disabled: busy,
    onDropRejected: (rejections) => toast.error(rejectionMessage(rejections)),
    onDropAccepted: ([file]) => {
      if (!file) return
      setPreview(URL.createObjectURL(file))
      onUpload(file).finally(() => setPreview(null))
    },
  })

  const shown = preview ?? imageUrl ?? null
  const rounded = shape === 'circle' ? 'rounded-full' : 'rounded-xl'

  return (
    <div className="flex flex-col items-start gap-5 sm:flex-row sm:items-center">
      <div
        {...getRootProps({
          className: cn(
            'group relative flex size-24 shrink-0 items-center justify-center overflow-hidden border-2 border-dashed transition-colors',
            rounded,
            isDragActive ? 'border-primary bg-primary/10' : 'border-border bg-muted/50',
          ),
        })}
      >
        <input {...getInputProps({ 'aria-label': `Seleccionar ${label}` })} />
        {shown ? (
          <img src={shown} alt={`Vista previa: ${label}`} className={cn('size-full object-cover', rounded)} />
        ) : (
          fallback
        )}
        {busy && (
          <div className={cn('bg-background/70 absolute inset-0 flex items-center justify-center', rounded)}>
            <Loader2 className="text-primary size-6 animate-spin" aria-hidden />
            <span className="sr-only">{uploading ? 'Subiendo imagen…' : 'Quitando imagen…'}</span>
          </div>
        )}
      </div>
      <div className="space-y-2">
        <div className="flex flex-wrap gap-2">
          <Button type="button" variant="outline" size="sm" onClick={open} disabled={busy}>
            <ImageUp /> {imageUrl ? 'Cambiar imagen' : 'Subir imagen'}
          </Button>
          {imageUrl && (
            <Button type="button" variant="ghost" size="sm" onClick={onRemove} disabled={busy}>
              <Trash2 /> Quitar
            </Button>
          )}
        </div>
        <p className="text-muted-foreground text-xs">
          Arrastra una imagen sobre el recuadro o elige un archivo. JPG, PNG, WEBP o GIF de hasta 5 MB.
        </p>
      </div>
    </div>
  )
}
