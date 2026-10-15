import { ImageOff, Package, Star } from 'lucide-react'
import { useState } from 'react'
import { Badge } from '@/components/ui/badge'
import { useMoney } from '@/lib/auth/session'
import { cn } from '@/lib/utils'
import { availabilityOf, imageSrc, type Availability } from '../lib'

const availabilityMeta: Record<Availability, { label: (stock: number) => string; className: string }> = {
  inStock: {
    label: (stock) => `En stock · ${stock}`,
    className: 'bg-success/10 text-success border-success/20',
  },
  lowStock: {
    label: (stock) => `Stock bajo · ${stock}`,
    className: 'bg-warning/15 text-warning border-warning/30',
  },
  outOfStock: {
    label: () => 'Agotado',
    className: 'bg-destructive/10 text-destructive border-destructive/20',
  },
  inactive: {
    label: () => 'No disponible',
    className: 'bg-muted text-muted-foreground border-border',
  },
}

/** Disponibilidad según estado y stock: verde (en stock), ámbar (1-5), rojo (agotado), gris (oculto). */
export function AvailabilityBadge({ isActive, stock, className }: { isActive: boolean; stock: number; className?: string }) {
  const kind = availabilityOf({ isActive, stock })
  const meta = availabilityMeta[kind]
  return (
    <Badge variant="outline" className={cn('gap-1.5', meta.className, className)}>
      <span className="size-1.5 rounded-full bg-current" aria-hidden />
      {meta.label(stock)}
    </Badge>
  )
}

export function CategoryBadge({ name }: { name: string | null | undefined }) {
  if (!name) return <span className="text-muted-foreground text-sm">Sin categoría</span>
  return (
    <Badge variant="secondary" className="max-w-40 truncate">
      {name}
    </Badge>
  )
}

/** Precio con el anterior tachado si existe. */
export function ProductPrice({
  price,
  priceOriginal,
  className,
}: {
  price: number
  priceOriginal?: number | null
  className?: string
}) {
  const money = useMoney()
  const hasDiscount = priceOriginal != null && priceOriginal > price
  return (
    <div className={cn('flex flex-col leading-tight tabular-nums', className)}>
      <span className="font-medium">{money(price)}</span>
      {hasDiscount && (
        <span className="text-muted-foreground text-xs line-through">
          <span className="sr-only">Precio anterior: </span>
          {money(priceOriginal)}
        </span>
      )}
    </div>
  )
}

/** Miniatura del producto con respaldo si no hay imagen o falla la carga. */
export function ProductThumb({
  url,
  alt,
  className,
  featured,
}: {
  url: string | null | undefined
  alt: string
  className?: string
  featured?: boolean
}) {
  const [failedUrl, setFailedUrl] = useState<string | null>(null)
  const src = imageSrc(url)
  const showImage = !!src && failedUrl !== src
  return (
    <div className={cn('bg-muted relative flex size-10 shrink-0 items-center justify-center overflow-hidden rounded-md border', className)}>
      {showImage ? (
        <img
          src={src}
          alt={alt}
          loading="lazy"
          decoding="async"
          className="size-full object-cover"
          onError={() => setFailedUrl(src)}
        />
      ) : src ? (
        <ImageOff className="text-muted-foreground size-1/3 min-h-4 min-w-4" aria-hidden />
      ) : (
        <Package className="text-muted-foreground size-1/3 min-h-4 min-w-4" aria-hidden />
      )}
      {featured && (
        <span
          className="bg-background/90 text-warning absolute top-0.5 right-0.5 rounded-full p-0.5 shadow-sm"
          title="Destacado"
        >
          <Star className="size-2.5 fill-current" aria-hidden />
          <span className="sr-only">Destacado</span>
        </span>
      )}
    </div>
  )
}
