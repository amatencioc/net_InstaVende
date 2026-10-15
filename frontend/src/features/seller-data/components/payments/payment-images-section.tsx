import { useQuery } from '@tanstack/react-query'
import { ImageIcon, ImagePlus, Loader2, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useDropzone, type FileRejection } from 'react-dropzone'
import { toast } from 'sonner'
import { paymentImagesQuery, useDeletePaymentImage, useUploadPaymentImage, type PaymentImage } from '../../api'
import { PAYMENT_IMAGE_MAX_BYTES, PAYMENT_IMAGES_MAX } from '../../meta'
import { useConfirm } from '@/components/shared/confirm-dialog'
import { QueryError } from '@/components/shared/query-state'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { apiBaseUrl } from '@/lib/api/client'
import { cn } from '@/lib/utils'

const ACCEPT = { 'image/png': ['.png'], 'image/jpeg': ['.jpg', '.jpeg'], 'image/webp': ['.webp'] }

function rejectionMessage({ file, errors }: FileRejection): string {
  const code = errors[0]?.code
  if (code === 'file-too-large') return `"${file.name}" supera los 5 MB.`
  if (code === 'file-invalid-type') return `"${file.name}" no es PNG, JPG ni WEBP.`
  if (code === 'too-many-files') return `Solo puedes tener ${PAYMENT_IMAGES_MAX} imágenes de pago.`
  return `No se pudo leer "${file.name}".`
}

/** Imágenes de apoyo para pagar (QR, capturas de cuenta): subida con arrastrar y soltar, miniaturas y borrado. */
export function PaymentImagesSection() {
  const query = useQuery(paymentImagesQuery)
  const upload = useUploadPaymentImage()
  const remove = useDeletePaymentImage()
  const confirm = useConfirm()
  const [pending, setPending] = useState(0)

  const images = query.data ?? []
  const remaining = Math.max(0, PAYMENT_IMAGES_MAX - images.length - pending)

  const uploadFiles = async (files: File[]) => {
    const accepted = files.slice(0, remaining)
    if (files.length > accepted.length)
      toast.warning(`Solo se subirán ${accepted.length} de ${files.length}: el máximo es ${PAYMENT_IMAGES_MAX} imágenes.`)
    if (accepted.length === 0) return
    setPending((p) => p + accepted.length)
    let ok = 0
    for (const file of accepted) {
      try {
        await upload.mutateAsync(file)
        ok++
      } catch {
        // El toast global ya muestra el motivo
      } finally {
        setPending((p) => p - 1)
      }
    }
    if (ok > 0) toast.success(ok === 1 ? 'Imagen subida.' : `${ok} imágenes subidas.`)
  }

  const { getRootProps, getInputProps, isDragActive } = useDropzone({
    accept: ACCEPT,
    maxSize: PAYMENT_IMAGE_MAX_BYTES,
    multiple: true,
    disabled: remaining === 0 || query.isPending,
    onDropAccepted: (files) => void uploadFiles(files),
    onDropRejected: (rejections) => rejections.slice(0, 3).forEach((r) => toast.error(rejectionMessage(r))),
  })

  const handleDelete = async (image: PaymentImage) => {
    const ok = await confirm({
      title: 'Eliminar imagen de pago',
      description: 'La imagen dejará de estar disponible para tus clientes. Esta acción no se puede deshacer.',
      confirmLabel: 'Eliminar',
      tone: 'destructive',
    })
    if (ok) remove.mutate(image.id)
  }

  return (
    <Card role="region" className="gap-4 px-4 py-5 sm:px-6" aria-labelledby="payment-images-title">
      <div className="flex flex-col gap-1 sm:flex-row sm:items-start sm:justify-between">
        <div className="space-y-0.5">
          <h2 id="payment-images-title" className="font-semibold">
            Imágenes de pago <span className="text-muted-foreground font-normal">(opcional)</span>
          </h2>
          <p className="text-muted-foreground text-sm">
            Sube códigos QR, capturas de cuenta o referencias visuales para facilitar el pago.
          </p>
        </div>
        <p className="text-muted-foreground shrink-0 text-xs tabular-nums" aria-live="polite">
          {images.length} de {PAYMENT_IMAGES_MAX} imágenes
        </p>
      </div>

      {query.isError ? (
        <QueryError error={query.error} onRetry={() => void query.refetch()} />
      ) : query.isPending ? (
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
          {Array.from({ length: 2 }, (_, i) => (
            <Skeleton key={i} className="aspect-square rounded-lg" />
          ))}
        </div>
      ) : images.length === 0 && pending === 0 ? (
        <div
          {...getRootProps({
            className: cn(
              'hover:border-primary/60 hover:bg-primary/5 focus-visible:ring-ring/50 flex cursor-pointer flex-col items-center gap-2 rounded-xl border-2 border-dashed px-6 py-10 text-center transition-colors outline-none focus-visible:ring-[3px]',
              isDragActive && 'border-primary bg-primary/5',
            ),
            'aria-label': 'Subir imágenes de pago',
          })}
        >
          <input {...getInputProps()} />
          <div className="bg-primary/10 text-primary flex size-11 items-center justify-center rounded-full">
            <ImagePlus className="size-5" aria-hidden />
          </div>
          <p className="text-sm font-medium">
            {isDragActive ? 'Suelta las imágenes aquí' : 'Arrastra tus imágenes o haz clic para subirlas'}
          </p>
          <p className="text-muted-foreground text-xs">
            PNG, JPG o WEBP · máx. {PAYMENT_IMAGES_MAX} imágenes · 5 MB por imagen
          </p>
        </div>
      ) : (
        <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5" aria-label="Imágenes de pago">
          {images.map((image, index) => (
            <li key={image.id} className="group/img bg-muted relative aspect-square overflow-hidden rounded-lg border">
              <a
                href={`${apiBaseUrl}${image.url}`}
                target="_blank"
                rel="noopener noreferrer"
                className="focus-visible:ring-ring/50 block size-full outline-none focus-visible:ring-[3px]"
                aria-label={`Ver imagen de pago ${index + 1} en tamaño completo`}
              >
                <img
                  src={`${apiBaseUrl}${image.url}`}
                  alt={`Imagen de pago ${index + 1}`}
                  loading="lazy"
                  className="size-full object-cover transition-transform group-hover/img:scale-[1.03]"
                />
              </a>
              <Button
                variant="secondary"
                size="icon-sm"
                onClick={() => void handleDelete(image)}
                aria-label={`Eliminar imagen de pago ${index + 1}`}
                className="hover:text-destructive absolute top-2 right-2 shadow-sm transition-opacity sm:opacity-0 sm:group-hover/img:opacity-100 sm:focus-visible:opacity-100"
              >
                <Trash2 aria-hidden />
              </Button>
            </li>
          ))}
          {Array.from({ length: pending }, (_, i) => (
            <li
              key={`pending-${i}`}
              className="bg-muted/60 flex aspect-square flex-col items-center justify-center gap-2 rounded-lg border"
              aria-label="Subiendo imagen"
            >
              <Loader2 className="text-muted-foreground size-5 animate-spin" aria-hidden />
              <span className="text-muted-foreground text-xs">Subiendo…</span>
            </li>
          ))}
          {remaining > 0 && (
            <li>
              <div
                {...getRootProps({
                  className: cn(
                    'hover:border-primary/60 hover:bg-primary/5 focus-visible:ring-ring/50 text-muted-foreground flex aspect-square cursor-pointer flex-col items-center justify-center gap-1.5 rounded-lg border-2 border-dashed p-2 text-center text-xs transition-colors outline-none focus-visible:ring-[3px]',
                    isDragActive && 'border-primary bg-primary/5',
                  ),
                  'aria-label': 'Agregar imagen de pago',
                })}
              >
                <input {...getInputProps()} />
                <ImagePlus className="size-5" aria-hidden />
                <span>Agregar imagen</span>
                <span className="text-[10px]">PNG, JPG o WEBP · 5 MB</span>
              </div>
            </li>
          )}
        </ul>
      )}

      {images.length >= PAYMENT_IMAGES_MAX && (
        <p className="text-muted-foreground flex items-center gap-2 text-xs">
          <ImageIcon className="size-3.5" aria-hidden />
          Alcanzaste el máximo de {PAYMENT_IMAGES_MAX} imágenes. Elimina alguna para subir otra.
        </p>
      )}
    </Card>
  )
}
