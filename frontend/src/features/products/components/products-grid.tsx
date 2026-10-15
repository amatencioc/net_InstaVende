import type { RowSelectionState } from '@tanstack/react-table'
import { Link } from '@tanstack/react-router'
import { Layers } from 'lucide-react'
import type { Dispatch, SetStateAction } from 'react'
import { Checkbox } from '@/components/ui/checkbox'
import { cn } from '@/lib/utils'
import type { ProductListItem } from '../api'
import { AvailabilityBadge, ProductPrice, ProductThumb } from './product-bits'
import { ProductRowActions } from './product-row-actions'

/** Vista en tarjetas del catálogo (comparte la selección con la tabla). */
export function ProductsGrid({
  data,
  rowSelection,
  onRowSelectionChange,
  fetching,
}: {
  data: ProductListItem[]
  rowSelection: RowSelectionState
  onRowSelectionChange: Dispatch<SetStateAction<RowSelectionState>>
  fetching?: boolean
}) {
  const toggle = (id: number, checked: boolean) =>
    onRowSelectionChange((prev) => {
      const next = { ...prev }
      if (checked) next[String(id)] = true
      else delete next[String(id)]
      return next
    })

  return (
    <ul
      className={cn(
        'grid grid-cols-2 gap-3 transition-opacity sm:grid-cols-3 sm:gap-4 lg:grid-cols-4 xl:grid-cols-5',
        fetching && 'opacity-70',
      )}
      aria-label="Productos"
    >
      {data.map((p) => {
        const selected = !!rowSelection[String(p.id)]
        return (
          <li
            key={p.id}
            className={cn(
              'group bg-card relative flex flex-col overflow-hidden rounded-xl border transition-shadow hover:shadow-md',
              selected && 'ring-primary ring-2',
              !p.isActive && 'opacity-75',
            )}
          >
            <Link
              to="/products/$productId"
              params={{ productId: String(p.id) }}
              className="focus-visible:ring-ring/50 block outline-none focus-visible:ring-[3px]"
              aria-label={p.name}
            >
              <ProductThumb
                url={p.imageUrl}
                alt=""
                featured={p.isFeatured}
                className="aspect-square size-auto w-full rounded-none border-0 border-b"
              />
            </Link>
            <div className="absolute top-2 left-2">
              <Checkbox
                aria-label={`Seleccionar ${p.name}`}
                checked={selected}
                onCheckedChange={(v) => toggle(p.id, v === true)}
                className="bg-background/90 size-5 shadow-sm"
              />
            </div>
            <div className="absolute top-1.5 right-1.5 opacity-100 transition-opacity sm:opacity-0 sm:group-focus-within:opacity-100 sm:group-hover:opacity-100">
              <ProductRowActions product={p} className="bg-background/90 shadow-sm" />
            </div>
            <div className="flex flex-1 flex-col gap-2 p-3">
              <div className="min-w-0 space-y-0.5">
                <Link
                  to="/products/$productId"
                  params={{ productId: String(p.id) }}
                  className="hover:text-primary line-clamp-2 text-sm leading-snug font-medium"
                  tabIndex={-1}
                >
                  {p.name}
                </Link>
                <p className="text-muted-foreground flex items-center gap-2 truncate text-xs">
                  <span className="truncate">{p.categoryName ?? 'Sin categoría'}</span>
                  {p.variantCount > 0 && (
                    <span className="inline-flex shrink-0 items-center gap-0.5">
                      <Layers className="size-3" aria-hidden />
                      {p.variantCount}
                    </span>
                  )}
                </p>
              </div>
              <div className="mt-auto flex flex-wrap items-end justify-between gap-2">
                <ProductPrice price={p.price} priceOriginal={p.priceOriginal} />
                <AvailabilityBadge isActive={p.isActive} stock={p.stock} />
              </div>
            </div>
          </li>
        )
      })}
    </ul>
  )
}
