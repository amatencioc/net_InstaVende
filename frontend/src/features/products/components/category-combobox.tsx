import { useQuery } from '@tanstack/react-query'
import { Check, ChevronsUpDown, Loader2, Plus } from 'lucide-react'
import { forwardRef, useState } from 'react'
import { Button } from '@/components/ui/button'
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { cn } from '@/lib/utils'
import { productCategoriesQuery, useCreateCategory } from '../api'

interface CategoryComboboxProps {
  value: number | null
  onChange: (value: number | null) => void
  id?: string
  'aria-invalid'?: boolean
  'aria-describedby'?: string
}

/** Selector de categoría con búsqueda y creación rápida ("Crear «…»"). */
export const CategoryCombobox = forwardRef<HTMLButtonElement, CategoryComboboxProps>(function CategoryCombobox(
  { value, onChange, ...aria },
  ref,
) {
  const [open, setOpen] = useState(false)
  const [search, setSearch] = useState('')
  const categories = useQuery(productCategoriesQuery)
  const create = useCreateCategory()

  const list = categories.data ?? []
  const selected = list.find((c) => c.id === value)
  const term = search.trim()
  const exists = list.some((c) => c.name.localeCompare(term, 'es', { sensitivity: 'base' }) === 0)

  const select = (id: number | null) => {
    onChange(id)
    setOpen(false)
    setSearch('')
  }

  const createAndSelect = () => {
    if (!term || term.length > 100) return
    create.mutate({ name: term }, { onSuccess: (category) => select(category.id) })
  }

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          ref={ref}
          type="button"
          variant="outline"
          role="combobox"
          aria-expanded={open}
          className="w-full justify-between font-normal"
          {...aria}
        >
          <span className={cn('truncate', !selected && 'text-muted-foreground')}>
            {selected?.name ?? (value != null && categories.isPending ? 'Cargando…' : 'Sin categoría')}
          </span>
          <ChevronsUpDown className="opacity-50" />
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-(--radix-popover-trigger-width) min-w-64 p-0" align="start">
        <Command>
          <CommandInput
            placeholder="Buscar o crear categoría…"
            value={search}
            onValueChange={setSearch}
            maxLength={100}
            onKeyDown={(e) => {
              // Enter sin coincidencias: crea la categoría escrita
              if (e.key === 'Enter' && term && !exists && !list.some((c) => c.name.toLowerCase().includes(term.toLowerCase()))) {
                e.preventDefault()
                createAndSelect()
              }
            }}
          />
          <CommandList>
            {categories.isPending ? (
              <div className="text-muted-foreground flex items-center justify-center gap-2 py-6 text-sm">
                <Loader2 className="size-4 animate-spin" /> Cargando…
              </div>
            ) : (
              <>
                <CommandEmpty>{term ? 'No hay coincidencias.' : 'Aún no tienes categorías.'}</CommandEmpty>
                <CommandGroup>
                  <CommandItem value="__none__ sin categoria" onSelect={() => select(null)}>
                    <Check className={cn(value == null ? 'opacity-100' : 'opacity-0')} />
                    <span className="text-muted-foreground">Sin categoría</span>
                  </CommandItem>
                  {list.map((c) => (
                    <CommandItem key={c.id} value={`${c.name} ${c.id}`} onSelect={() => select(c.id)}>
                      <Check className={cn(value === c.id ? 'opacity-100' : 'opacity-0')} />
                      <span className="truncate">{c.name}</span>
                    </CommandItem>
                  ))}
                </CommandGroup>
                {term && !exists && (
                  <CommandGroup forceMount>
                    <CommandItem
                      forceMount
                      value={`__create__ ${term}`}
                      onSelect={createAndSelect}
                      disabled={create.isPending}
                      className="text-primary"
                    >
                      {create.isPending ? <Loader2 className="animate-spin" /> : <Plus className="text-primary" />}
                      Crear «{term}»
                    </CommandItem>
                  </CommandGroup>
                )}
              </>
            )}
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  )
})
