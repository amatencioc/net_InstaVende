import { useQuery } from '@tanstack/react-query'
import type { RowSelectionState } from '@tanstack/react-table'
import { Link } from '@tanstack/react-router'
import {
  ChevronLeft,
  ChevronRight,
  Eye,
  EyeOff,
  FolderInput,
  LayoutGrid,
  List,
  Loader2,
  Package,
  Plus,
  Search,
  SearchX,
  Trash2,
  X,
} from 'lucide-react'
import { useEffect, useEffectEvent, useState } from 'react'
import { useConfirm } from '@/components/shared/confirm-dialog'
import { EmptyState } from '@/components/shared/empty-state'
import { PageContainer, PageHeader } from '@/components/shared/page-header'
import { QueryError } from '@/components/shared/query-state'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { Select, SelectContent, SelectItem, SelectSeparator, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group'
import { formatNumber } from '@/lib/format'
import { cn } from '@/lib/utils'
import {
  productCategoriesQuery,
  productListQuery,
  useBulkProducts,
  type ProductBulkAction,
  type ProductSort,
  type ProductStatusFilter,
} from '../api'
import { useDebouncedValue } from '../hooks'
import { plural, sortLabels, statusLabels } from '../lib'
import {
  hasActiveFilters,
  PAGE_SIZES,
  productSortValues,
  productStatusValues,
  toListParams,
  type ProductSearch,
} from '../schemas'
import { CategoriesDialog } from './categories-dialog'
import { ProductsGrid } from './products-grid'
import { ProductsTable } from './products-table'

export type ProductSearchUpdate = (patch: Partial<ProductSearch>, options?: { replace?: boolean }) => void

/** Listado del catálogo: búsqueda, filtros, ordenación, selección múltiple y dos vistas. */
export function ProductsPage({ search, onSearchChange }: { search: ProductSearch; onSearchChange: ProductSearchUpdate }) {
  const params = toListParams(search)
  const list = useQuery(productListQuery(params))
  const [rowSelection, setRowSelection] = useState<RowSelectionState>({})
  const [searchKey, setSearchKey] = useState(0)
  const view = search.view ?? 'table'
  const sort: ProductSort = search.sort ?? 'newest'
  const filtered = hasActiveFilters(search)
  const selectedIds = Object.keys(rowSelection)
    .filter((k) => rowSelection[k])
    .map(Number)

  // Si la página actual quedó vacía (p.ej. tras eliminar), vuelve a la última con datos
  const fixPage = useEffectEvent((totalPages: number) =>
    onSearchChange({ page: totalPages > 1 ? totalPages : undefined }, { replace: true }),
  )
  const data = list.data
  useEffect(() => {
    if (data && data.items.length === 0 && data.page > 1 && !list.isPlaceholderData) fixPage(data.totalPages ?? 1)
  }, [data, list.isPlaceholderData])

  const clearFilters = () => {
    setSearchKey((k) => k + 1)
    onSearchChange({ q: undefined, category: undefined, status: undefined, page: undefined })
  }

  return (
    <PageContainer>
      <PageHeader
        title="Productos"
        description="Visualiza, edita y gestiona el catálogo que ofrece tu vendedor IA."
        actions={
          <>
            <CategoriesDialog />
            <Button asChild>
              <Link to="/products/new">
                <Plus /> Nuevo producto
              </Link>
            </Button>
          </>
        }
      />

      <div className="flex flex-col gap-3">
        <div className="flex flex-col gap-2 lg:flex-row lg:items-center">
          <SearchInput
            key={searchKey}
            initial={search.q ?? ''}
            onCommit={(q) => {
              if ((q || undefined) !== search.q) onSearchChange({ q: q || undefined, page: undefined }, { replace: true })
            }}
          />
          <div className="grid grid-cols-2 gap-2 sm:flex sm:flex-wrap sm:items-center">
            <CategoryFilter
              value={search.category}
              onChange={(category) => onSearchChange({ category, page: undefined })}
            />
            <Select
              value={search.status ?? 'all'}
              onValueChange={(v) =>
                onSearchChange({ status: v === 'all' ? undefined : (v as ProductStatusFilter), page: undefined })
              }
            >
              <SelectTrigger className="w-full sm:w-40" aria-label="Filtrar por estado">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">Todos los estados</SelectItem>
                <SelectSeparator />
                {productStatusValues.map((s) => (
                  <SelectItem key={s} value={s}>
                    {statusLabels[s]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Select value={sort} onValueChange={(v) => onSearchChange({ sort: v === 'newest' ? undefined : (v as ProductSort) })}>
              <SelectTrigger className="w-full sm:w-48" aria-label="Ordenar">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {productSortValues.map((s) => (
                  <SelectItem key={s} value={s}>
                    {sortLabels[s]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <ToggleGroup
              type="single"
              variant="outline"
              value={view}
              onValueChange={(v) => v && onSearchChange({ view: v === 'table' ? undefined : 'grid' }, { replace: true })}
              aria-label="Vista"
              className="justify-self-end"
            >
              <ToggleGroupItem value="table" aria-label="Vista de tabla">
                <List />
              </ToggleGroupItem>
              <ToggleGroupItem value="grid" aria-label="Vista de tarjetas">
                <LayoutGrid />
              </ToggleGroupItem>
            </ToggleGroup>
          </div>
        </div>

        {filtered && (
          <div className="flex flex-wrap items-center gap-2 text-sm">
            <span className="text-muted-foreground">
              {list.data ? `${plural(list.data.totalCount, 'resultado', 'resultados')}` : 'Buscando…'}
            </span>
            <Button variant="ghost" size="xs" onClick={clearFilters}>
              <X /> Limpiar filtros
            </Button>
          </div>
        )}
      </div>

      {selectedIds.length > 0 && (
        <BulkActionsBar ids={selectedIds} onClear={() => setRowSelection({})} />
      )}

      {list.isPending ? (
        view === 'grid' ? (
          <GridSkeleton />
        ) : (
          <TableSkeleton />
        )
      ) : list.isError ? (
        <QueryError error={list.error} onRetry={() => void list.refetch()} />
      ) : list.data.totalCount === 0 ? (
        filtered ? (
          <EmptyState
            icon={SearchX}
            title="No encontramos productos"
            description="Prueba con otra búsqueda o ajusta los filtros."
            action={
              <Button variant="outline" onClick={clearFilters}>
                Limpiar filtros
              </Button>
            }
          />
        ) : (
          <EmptyState
            icon={Package}
            title="Aún no tienes productos"
            description="Crea tu primer producto para que tu vendedor IA pueda ofrecerlo y venderlo por tus canales."
            action={
              <Button asChild>
                <Link to="/products/new">
                  <Plus /> Crear producto
                </Link>
              </Button>
            }
          />
        )
      ) : (
        <>
          {view === 'grid' ? (
            <ProductsGrid
              data={list.data.items}
              rowSelection={rowSelection}
              onRowSelectionChange={setRowSelection}
              fetching={list.isPlaceholderData}
            />
          ) : (
            <ProductsTable
              data={list.data.items}
              sort={sort}
              onSort={(s) => onSearchChange({ sort: s === 'newest' ? undefined : s })}
              rowSelection={rowSelection}
              onRowSelectionChange={setRowSelection}
              fetching={list.isPlaceholderData}
            />
          )}
          <Pagination
            page={list.data.page}
            pageSize={list.data.pageSize}
            total={list.data.totalCount}
            totalPages={list.data.totalPages ?? Math.ceil(list.data.totalCount / list.data.pageSize)}
            onPage={(page) => onSearchChange({ page: page > 1 ? page : undefined })}
            onPageSize={(pageSize) => onSearchChange({ pageSize: pageSize === 20 ? undefined : pageSize, page: undefined })}
          />
        </>
      )}
    </PageContainer>
  )
}

// ── Piezas ───────────────────────────────────────────────────────────────────

function SearchInput({ initial, onCommit }: { initial: string; onCommit: (value: string) => void }) {
  const [text, setText] = useState(initial)
  const debounced = useDebouncedValue(text.trim())
  const commit = useEffectEvent((value: string) => onCommit(value))
  useEffect(() => {
    commit(debounced)
  }, [debounced])

  return (
    <div className="relative flex-1">
      <Search className="text-muted-foreground pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2" aria-hidden />
      <Input
        type="search"
        value={text}
        onChange={(e) => setText(e.target.value)}
        placeholder="Buscar por nombre o SKU…"
        aria-label="Buscar productos"
        maxLength={100}
        className="pl-9"
      />
    </div>
  )
}

function CategoryFilter({ value, onChange }: { value: number | undefined; onChange: (value: number | undefined) => void }) {
  const categories = useQuery(productCategoriesQuery)
  const selectValue = value === undefined ? 'all' : String(value)
  return (
    <Select value={selectValue} onValueChange={(v) => onChange(v === 'all' ? undefined : Number(v))}>
      <SelectTrigger className="w-full sm:w-48" aria-label="Filtrar por categoría">
        <SelectValue placeholder="Todas las categorías">
          {value === undefined
            ? 'Todas las categorías'
            : value === 0
              ? 'Sin categoría'
              : (categories.data?.find((c) => c.id === value)?.name ?? 'Categoría')}
        </SelectValue>
      </SelectTrigger>
      <SelectContent>
        <SelectItem value="all">Todas las categorías</SelectItem>
        <SelectItem value="0">Sin categoría</SelectItem>
        {(categories.data?.length ?? 0) > 0 && <SelectSeparator />}
        {categories.data?.map((c) => (
          <SelectItem key={c.id} value={String(c.id)}>
            <span className="truncate">{c.name}</span>
            <span className="text-muted-foreground ml-auto pl-2 text-xs tabular-nums">{c.productCount}</span>
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}

function BulkActionsBar({ ids, onClear }: { ids: number[]; onClear: () => void }) {
  const bulk = useBulkProducts()
  const confirm = useConfirm()
  const categories = useQuery(productCategoriesQuery)

  const run = (action: ProductBulkAction, categoryId?: number | null) =>
    bulk.mutate({ ids, action, categoryId: categoryId ?? null }, { onSuccess: onClear })

  const onDelete = async () => {
    const ok = await confirm({
      title: `¿Eliminar ${plural(ids.length, 'producto', 'productos')}?`,
      description:
        'Los productos sin pedidos se eliminarán definitivamente junto con sus imágenes. Los que figuran en pedidos se marcarán como no disponibles para conservar el historial.',
      confirmLabel: 'Eliminar',
      tone: 'destructive',
    })
    if (ok) run('delete')
  }

  return (
    <div
      role="region"
      aria-label="Acciones masivas"
      className="bg-primary/5 border-primary/30 animate-in fade-in-0 slide-in-from-top-1 sticky top-18 z-10 flex flex-wrap items-center gap-2 rounded-xl border px-3 py-2 backdrop-blur"
    >
      <span className="mr-auto text-sm font-medium" aria-live="polite">
        {plural(ids.length, 'producto seleccionado', 'productos seleccionados')}
      </span>
      {bulk.isPending && <Loader2 className="text-muted-foreground size-4 animate-spin" aria-label="Procesando" />}
      <Button size="sm" variant="outline" disabled={bulk.isPending} onClick={() => run('activate')}>
        <Eye /> <span className="hidden sm:inline">Activar</span>
      </Button>
      <Button size="sm" variant="outline" disabled={bulk.isPending} onClick={() => run('deactivate')}>
        <EyeOff /> <span className="hidden sm:inline">Desactivar</span>
      </Button>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button size="sm" variant="outline" disabled={bulk.isPending}>
            <FolderInput /> <span className="hidden sm:inline">Categoría</span>
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="max-h-72 w-56 overflow-y-auto">
          <DropdownMenuLabel>Mover a categoría</DropdownMenuLabel>
          {categories.data?.length === 0 && (
            <DropdownMenuItem disabled>No hay categorías</DropdownMenuItem>
          )}
          {categories.data?.map((c) => (
            <DropdownMenuItem key={c.id} onSelect={() => run('setCategory', c.id)}>
              <span className="truncate">{c.name}</span>
            </DropdownMenuItem>
          ))}
          <DropdownMenuSeparator />
          <DropdownMenuItem onSelect={() => run('setCategory', null)}>Quitar categoría</DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
      <Button size="sm" variant="outline" className="text-destructive hover:text-destructive" disabled={bulk.isPending} onClick={() => void onDelete()}>
        <Trash2 /> <span className="hidden sm:inline">Eliminar</span>
      </Button>
      <Button size="icon-sm" variant="ghost" onClick={onClear} aria-label="Quitar selección">
        <X />
      </Button>
    </div>
  )
}

function Pagination({
  page,
  pageSize,
  total,
  totalPages,
  onPage,
  onPageSize,
}: {
  page: number
  pageSize: number
  total: number
  totalPages: number
  onPage: (page: number) => void
  onPageSize: (size: number) => void
}) {
  const from = total === 0 ? 0 : (page - 1) * pageSize + 1
  const to = Math.min(page * pageSize, total)
  const pages = pageWindow(page, totalPages)

  return (
    <nav aria-label="Paginación" className="flex flex-col-reverse items-center justify-between gap-3 sm:flex-row">
      <div className="text-muted-foreground flex items-center gap-3 text-sm">
        <span>
          {formatNumber(from)}–{formatNumber(to)} de {plural(total, 'producto', 'productos')}
        </span>
        <span className="hidden items-center gap-2 sm:flex">
          <span aria-hidden>·</span>
          <label htmlFor="products-page-size">Filas</label>
          <Select value={String(pageSize)} onValueChange={(v) => onPageSize(Number(v))}>
            <SelectTrigger id="products-page-size" size="sm" className="w-18">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {PAGE_SIZES.map((s) => (
                <SelectItem key={s} value={String(s)}>
                  {s}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </span>
      </div>
      {totalPages > 1 && (
        <div className="flex items-center gap-1">
          <Button variant="outline" size="icon-sm" disabled={page <= 1} onClick={() => onPage(page - 1)} aria-label="Página anterior">
            <ChevronLeft />
          </Button>
          {pages.map((p, i) =>
            p === null ? (
              <span key={`gap-${i}`} className="text-muted-foreground px-1 text-sm" aria-hidden>
                …
              </span>
            ) : (
              <Button
                key={p}
                variant={p === page ? 'default' : 'ghost'}
                size="icon-sm"
                onClick={() => onPage(p)}
                aria-label={`Página ${p}`}
                aria-current={p === page ? 'page' : undefined}
                className={cn('tabular-nums', p !== page && 'hidden sm:inline-flex')}
              >
                {p}
              </Button>
            ),
          )}
          <Button
            variant="outline"
            size="icon-sm"
            disabled={page >= totalPages}
            onClick={() => onPage(page + 1)}
            aria-label="Página siguiente"
          >
            <ChevronRight />
          </Button>
        </div>
      )}
    </nav>
  )
}

/** Páginas visibles: 1 … 4 5 6 … 12 (null = salto). */
function pageWindow(page: number, totalPages: number): (number | null)[] {
  if (totalPages <= 7) return Array.from({ length: totalPages }, (_, i) => i + 1)
  const out: (number | null)[] = [1]
  const start = Math.max(2, page - 1)
  const end = Math.min(totalPages - 1, page + 1)
  if (start > 2) out.push(null)
  for (let p = start; p <= end; p++) out.push(p)
  if (end < totalPages - 1) out.push(null)
  out.push(totalPages)
  return out
}

function TableSkeleton() {
  return (
    <div className="overflow-hidden rounded-xl border" aria-busy="true" aria-label="Cargando productos">
      <div className="bg-muted/40 h-10 border-b" />
      {Array.from({ length: 8 }, (_, i) => (
        <div key={i} className="flex items-center gap-4 border-b px-3 py-3 last:border-0">
          <Skeleton className="size-4 rounded" />
          <Skeleton className="size-11 rounded-md" />
          <div className="flex-1 space-y-2">
            <Skeleton className="h-4 w-2/5" />
            <Skeleton className="h-3 w-1/5" />
          </div>
          <Skeleton className="hidden h-5 w-20 rounded-full md:block" />
          <Skeleton className="hidden h-5 w-24 rounded-full sm:block" />
          <Skeleton className="h-4 w-16" />
        </div>
      ))}
    </div>
  )
}

function GridSkeleton() {
  return (
    <div
      className="grid grid-cols-2 gap-3 sm:grid-cols-3 sm:gap-4 lg:grid-cols-4 xl:grid-cols-5"
      aria-busy="true"
      aria-label="Cargando productos"
    >
      {Array.from({ length: 10 }, (_, i) => (
        <div key={i} className="overflow-hidden rounded-xl border">
          <Skeleton className="aspect-square w-full rounded-none" />
          <div className="space-y-2 p-3">
            <Skeleton className="h-4 w-4/5" />
            <Skeleton className="h-3 w-1/2" />
            <Skeleton className="h-4 w-1/3" />
          </div>
        </div>
      ))}
    </div>
  )
}
