import { createColumnHelper, rowSelectionFeature, tableFeatures, useTable, type RowSelectionState } from '@tanstack/react-table'
import { Link } from '@tanstack/react-router'
import { ArrowDown, ArrowUp, ArrowUpDown, Layers } from 'lucide-react'
import type { Dispatch, SetStateAction } from 'react'
import { Checkbox } from '@/components/ui/checkbox'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { formatDate } from '@/lib/format'
import { cn } from '@/lib/utils'
import type { ProductListItem, ProductSort } from '../api'
import { AvailabilityBadge, CategoryBadge, ProductPrice, ProductThumb } from './product-bits'
import { ProductRowActions } from './product-row-actions'

interface ProductsTableMeta {
  sort: ProductSort
  onSort: (sort: ProductSort) => void
}

interface ProductsColumnMeta {
  className?: string
}

const features = tableFeatures({
  rowSelectionFeature,
  tableMeta: {} as ProductsTableMeta,
  columnMeta: {} as ProductsColumnMeta,
})
const helper = createColumnHelper<typeof features, ProductListItem>()

/** Cabecera ordenable (la ordenación se hace en el servidor). */
function SortHeader({
  label,
  asc,
  desc,
  meta,
  align = 'left',
}: {
  label: string
  asc: ProductSort
  desc: ProductSort
  meta: ProductsTableMeta | undefined
  align?: 'left' | 'right'
}) {
  const current = meta?.sort
  const direction = current === asc ? 'asc' : current === desc ? 'desc' : null
  const Icon = direction === 'asc' ? ArrowUp : direction === 'desc' ? ArrowDown : ArrowUpDown
  return (
    <button
      type="button"
      onClick={() => meta?.onSort(direction === 'asc' ? desc : asc)}
      className={cn(
        'hover:text-foreground focus-visible:ring-ring/50 -mx-1 inline-flex items-center gap-1 rounded px-1 py-0.5 outline-none focus-visible:ring-[3px]',
        direction && 'text-foreground',
        align === 'right' && 'flex-row-reverse',
      )}
      aria-label={`Ordenar por ${label.toLowerCase()}`}
    >
      {label}
      <Icon className={cn('size-3.5', !direction && 'opacity-40')} aria-hidden />
    </button>
  )
}

const columns = helper.columns([
  helper.display({
    id: 'select',
    meta: { className: 'w-10 pr-0' },
    header: ({ table }) => (
      <Checkbox
        aria-label="Seleccionar todos los productos de la página"
        checked={table.getIsAllPageRowsSelected() ? true : table.getIsSomePageRowsSelected() ? 'indeterminate' : false}
        onCheckedChange={(value) => table.toggleAllPageRowsSelected(value === true)}
        className="data-[state=indeterminate]:border-primary data-[state=indeterminate]:bg-primary data-[state=indeterminate]:text-primary-foreground"
      />
    ),
    cell: ({ row }) => (
      <Checkbox
        aria-label={`Seleccionar ${row.original.name}`}
        checked={row.getIsSelected()}
        onCheckedChange={(value) => row.toggleSelected(value === true)}
      />
    ),
  }),
  helper.accessor('name', {
    header: ({ table }) => <SortHeader label="Producto" asc="nameAsc" desc="nameDesc" meta={table.options.meta} />,
    meta: { className: 'min-w-64' },
    cell: ({ row }) => {
      const p = row.original
      return (
        <div className="flex items-center gap-3">
          <ProductThumb url={p.imageUrl} alt="" featured={p.isFeatured} className="size-11" />
          <div className="min-w-0 space-y-0.5">
            <Link
              to="/products/$productId"
              params={{ productId: String(p.id) }}
              className="hover:text-primary line-clamp-2 font-medium focus-visible:underline focus-visible:outline-none"
            >
              {p.name}
            </Link>
            <p className="text-muted-foreground flex flex-wrap items-center gap-x-2 text-xs">
              {p.sku && <span className="font-mono">{p.sku}</span>}
              {p.variantCount > 0 && (
                <span className="inline-flex items-center gap-0.5">
                  <Layers className="size-3" aria-hidden />
                  {p.variantCount} {p.variantCount === 1 ? 'variante' : 'variantes'}
                </span>
              )}
              {!p.sku && p.variantCount === 0 && <span>Creado el {formatDate(p.createdAt)}</span>}
            </p>
          </div>
        </div>
      )
    },
  }),
  helper.accessor('categoryName', {
    header: 'Categoría',
    meta: { className: 'hidden md:table-cell' },
    cell: ({ row }) => <CategoryBadge name={row.original.categoryName} />,
  }),
  helper.accessor('stock', {
    header: ({ table }) => <SortHeader label="Disponibilidad" asc="stockAsc" desc="stockDesc" meta={table.options.meta} />,
    meta: { className: 'hidden sm:table-cell' },
    cell: ({ row }) => <AvailabilityBadge isActive={row.original.isActive} stock={row.original.stock} />,
  }),
  helper.accessor('price', {
    header: ({ table }) => (
      <SortHeader label="Precio" asc="priceAsc" desc="priceDesc" meta={table.options.meta} align="right" />
    ),
    meta: { className: 'text-right' },
    cell: ({ row }) => (
      <ProductPrice price={row.original.price} priceOriginal={row.original.priceOriginal} className="items-end" />
    ),
  }),
  helper.display({
    id: 'actions',
    header: () => <span className="sr-only">Acciones</span>,
    meta: { className: 'w-12 text-right' },
    cell: ({ row }) => <ProductRowActions product={row.original} />,
  }),
])

interface ProductsTableProps {
  data: ProductListItem[]
  sort: ProductSort
  onSort: (sort: ProductSort) => void
  rowSelection: RowSelectionState
  onRowSelectionChange: Dispatch<SetStateAction<RowSelectionState>>
  fetching?: boolean
}

const getRowId = (row: ProductListItem) => String(row.id)

/** Tabla de productos con selección múltiple y ordenación por columnas. */
export function ProductsTable({ data, sort, onSort, rowSelection, onRowSelectionChange, fetching }: ProductsTableProps) {
  const table = useTable({
    features,
    columns,
    data,
    getRowId,
    state: { rowSelection },
    onRowSelectionChange,
    meta: { sort, onSort },
  })

  return (
    <div className={cn('bg-card overflow-hidden rounded-xl border transition-opacity', fetching && 'opacity-70')}>
      <Table>
        <TableHeader className="bg-muted/40">
          {table.getHeaderGroups().map((group) => (
            <TableRow key={group.id} className="hover:bg-transparent">
              {group.headers.map((header) => (
                <TableHead key={header.id} className={cn('text-xs', header.column.columnDef.meta?.className)}>
                  {header.isPlaceholder ? null : <table.FlexRender header={header} />}
                </TableHead>
              ))}
            </TableRow>
          ))}
        </TableHeader>
        <TableBody>
          {table.getRowModel().rows.map((row) => (
            <TableRow
              key={row.id}
              data-state={row.getIsSelected() ? 'selected' : undefined}
              className={cn(!row.original.isActive && 'text-muted-foreground')}
            >
              {row.getAllCells().map((cell) => (
                <TableCell key={cell.id} className={cn('py-3', cell.column.columnDef.meta?.className)}>
                  <table.FlexRender cell={cell} />
                </TableCell>
              ))}
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  )
}
