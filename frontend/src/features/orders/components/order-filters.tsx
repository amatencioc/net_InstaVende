import { ChevronDown, Download, Loader2, Search, X } from 'lucide-react'
import { useEffect, useId, useRef, useState } from 'react'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Switch } from '@/components/ui/switch'
import type { OrderFilters } from '../api'
import { useDebouncedValue } from '../hooks'

interface OrderFiltersBarProps {
  filters: OrderFilters
  onChange: (next: Partial<OrderFilters>) => void
  notify: boolean
  onNotifyChange: (value: boolean) => void
  onExport: (scope: 'tab' | 'all') => void
  exporting: boolean
  /** Nombre de la pestaña activa (para la opción de exportación). */
  tabLabel: string
}

/** Barra de filtros del tablero: búsqueda con debounce, rango de fechas, aviso al cliente y exportación. */
export function OrderFiltersBar({
  filters,
  onChange,
  notify,
  onNotifyChange,
  onExport,
  exporting,
  tabLabel,
}: OrderFiltersBarProps) {
  const ids = { search: useId(), from: useId(), to: useId(), notify: useId() }
  const [term, setTerm] = useState(filters.search ?? '')
  const debounced = useDebouncedValue(term.trim(), 350)

  // Si la búsqueda cambia desde fuera (limpiar filtros, atrás/adelante del navegador) se sincroniza el input
  const [syncedSearch, setSyncedSearch] = useState(filters.search)
  if (filters.search !== syncedSearch) {
    setSyncedSearch(filters.search)
    if ((filters.search ?? '') !== term.trim()) setTerm(filters.search ?? '')
  }

  // Solo se publica en la URL cuando cambia el término con debounce (no al limpiar desde fuera)
  const lastDebounced = useRef(debounced)
  useEffect(() => {
    if (lastDebounced.current === debounced) return
    lastDebounced.current = debounced
    if ((debounced || undefined) !== (filters.search || undefined)) onChange({ search: debounced || undefined })
  }, [debounced, filters.search, onChange])

  const hasFilters = Boolean(filters.search || filters.from || filters.to)

  const clearAll = () => {
    setTerm('')
    onChange({ search: undefined, from: undefined, to: undefined })
  }

  return (
    <div className="bg-card flex flex-col gap-3 rounded-xl border p-3 lg:flex-row lg:items-end">
      <div className="min-w-0 flex-1 space-y-1.5">
        <Label htmlFor={ids.search} className="text-muted-foreground text-xs">
          Buscar
        </Label>
        <div className="relative">
          <Search className="text-muted-foreground pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2" aria-hidden />
          <Input
            id={ids.search}
            type="search"
            value={term}
            onChange={(e) => setTerm(e.target.value)}
            placeholder="Número de pedido, cliente o teléfono…"
            className="pl-8"
            maxLength={100}
            autoComplete="off"
          />
        </div>
      </div>
      <div className="grid grid-cols-2 gap-3 sm:flex sm:items-end">
        <div className="space-y-1.5">
          <Label htmlFor={ids.from} className="text-muted-foreground text-xs">
            Desde
          </Label>
          <Input
            id={ids.from}
            type="date"
            value={filters.from ?? ''}
            max={filters.to}
            onChange={(e) => onChange({ from: e.target.value || undefined })}
            className="sm:w-40"
          />
        </div>
        <div className="space-y-1.5">
          <Label htmlFor={ids.to} className="text-muted-foreground text-xs">
            Hasta
          </Label>
          <Input
            id={ids.to}
            type="date"
            value={filters.to ?? ''}
            min={filters.from}
            onChange={(e) => onChange({ to: e.target.value || undefined })}
            className="sm:w-40"
          />
        </div>
        {hasFilters && (
          <Button variant="ghost" onClick={clearAll} className="col-span-2 sm:col-span-1">
            <X /> Limpiar
          </Button>
        )}
      </div>
      <div className="flex flex-wrap items-center justify-between gap-3 lg:justify-end">
        <div className="flex h-9 items-center gap-2">
          <Switch id={ids.notify} checked={notify} onCheckedChange={onNotifyChange} />
          <Label htmlFor={ids.notify} className="cursor-pointer text-sm font-normal">
            Avisar al cliente al mover
          </Label>
        </div>
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="outline" disabled={exporting}>
              {exporting ? <Loader2 className="animate-spin" /> : <Download />}
              Exportar CSV
              <ChevronDown className="opacity-60" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuLabel className="text-muted-foreground text-xs font-normal">
              Se aplican la búsqueda y las fechas
            </DropdownMenuLabel>
            <DropdownMenuItem onSelect={() => onExport('tab')}>Pedidos de «{tabLabel}»</DropdownMenuItem>
            <DropdownMenuItem onSelect={() => onExport('all')}>Todos los pedidos</DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </div>
  )
}
