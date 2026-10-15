import { useQuery } from '@tanstack/react-query'
import { Check, FolderOpen, Loader2, Pencil, Plus, Tags, Trash2, X } from 'lucide-react'
import { useState, type FormEvent, type ReactNode } from 'react'
import { useConfirm } from '@/components/shared/confirm-dialog'
import { EmptyState } from '@/components/shared/empty-state'
import { QueryError } from '@/components/shared/query-state'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import {
  productCategoriesQuery,
  useCreateCategory,
  useDeleteCategory,
  useUpdateCategory,
  type ProductCategory,
} from '../api'
import { plural } from '../lib'
import { categoryFormSchema } from '../schemas'

/** Diálogo de gestión de categorías: crear, renombrar y eliminar. */
export function CategoriesDialog({ trigger }: { trigger?: ReactNode }) {
  const [open, setOpen] = useState(false)
  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        {trigger ?? (
          <Button variant="outline">
            <Tags /> Categorías
          </Button>
        )}
      </DialogTrigger>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Categorías</DialogTitle>
          <DialogDescription>
            Organiza tu catálogo. Tu vendedor IA usa las categorías para recomendar alternativas.
          </DialogDescription>
        </DialogHeader>
        {open && <CategoriesManager />}
      </DialogContent>
    </Dialog>
  )
}

function CategoriesManager() {
  const categories = useQuery(productCategoriesQuery)
  const create = useCreateCategory()
  const [name, setName] = useState('')
  const [error, setError] = useState<string | null>(null)

  const onCreate = (e: FormEvent) => {
    e.preventDefault()
    const parsed = categoryFormSchema.safeParse({ name })
    if (!parsed.success) {
      setError(parsed.error.issues[0]?.message ?? 'Nombre no válido.')
      return
    }
    setError(null)
    create.mutate(parsed.data, {
      onSuccess: () => setName(''),
    })
  }

  return (
    <div className="space-y-4">
      <form onSubmit={onCreate} className="space-y-1.5" noValidate>
        <label htmlFor="new-category" className="sr-only">
          Nueva categoría
        </label>
        <div className="flex gap-2">
          <Input
            id="new-category"
            placeholder="Nueva categoría, p. ej. Calzado"
            value={name}
            maxLength={100}
            onChange={(e) => {
              setName(e.target.value)
              if (error) setError(null)
            }}
            aria-invalid={!!error}
            aria-describedby={error ? 'new-category-error' : undefined}
          />
          <Button type="submit" disabled={create.isPending || !name.trim()}>
            {create.isPending ? <Loader2 className="animate-spin" /> : <Plus />}
            Añadir
          </Button>
        </div>
        {error && (
          <p id="new-category-error" className="text-destructive text-sm">
            {error}
          </p>
        )}
      </form>

      {categories.isPending ? (
        <div className="space-y-2" aria-busy="true" aria-label="Cargando categorías">
          {Array.from({ length: 4 }, (_, i) => (
            <Skeleton key={i} className="h-11 w-full" />
          ))}
        </div>
      ) : categories.isError ? (
        <QueryError error={categories.error} onRetry={() => void categories.refetch()} />
      ) : categories.data.length === 0 ? (
        <EmptyState
          icon={FolderOpen}
          title="Aún no tienes categorías"
          description="Crea la primera para agrupar tus productos."
          className="py-8"
        />
      ) : (
        <div className="-mx-2 max-h-80 overflow-y-auto">
          <ul className="space-y-1 px-2" aria-label="Categorías">
            {categories.data.map((c) => (
              <CategoryRow key={c.id} category={c} />
            ))}
          </ul>
        </div>
      )}
    </div>
  )
}

function CategoryRow({ category }: { category: ProductCategory }) {
  const confirm = useConfirm()
  const update = useUpdateCategory()
  const remove = useDeleteCategory()
  const [editing, setEditing] = useState(false)
  const [name, setName] = useState(category.name)
  const [error, setError] = useState<string | null>(null)
  const inputId = `category-${category.id}`

  const save = (e: FormEvent) => {
    e.preventDefault()
    const parsed = categoryFormSchema.safeParse({ name })
    if (!parsed.success) {
      setError(parsed.error.issues[0]?.message ?? 'Nombre no válido.')
      return
    }
    if (parsed.data.name === category.name) {
      setEditing(false)
      return
    }
    update.mutate(
      { id: category.id, name: parsed.data.name, description: category.description },
      {
        onSuccess: () => {
          setEditing(false)
          setError(null)
        },
      },
    )
  }

  const onDelete = async () => {
    const ok = await confirm({
      title: `¿Eliminar «${category.name}»?`,
      description:
        category.productCount > 0
          ? `${plural(category.productCount, 'producto quedará', 'productos quedarán')} sin categoría. Los productos no se eliminan.`
          : 'La categoría no tiene productos.',
      confirmLabel: 'Eliminar categoría',
      tone: 'destructive',
    })
    if (ok) remove.mutate(category.id)
  }

  if (editing) {
    return (
      <li>
        <form onSubmit={save} className="space-y-1 py-1" noValidate>
          <label htmlFor={inputId} className="sr-only">
            Nombre de la categoría
          </label>
          <div className="flex items-center gap-2">
            <Input
              id={inputId}
              value={name}
              maxLength={100}
              autoFocus
              onChange={(e) => setName(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Escape') {
                  e.stopPropagation()
                  setEditing(false)
                  setName(category.name)
                  setError(null)
                }
              }}
              aria-invalid={!!error}
            />
            <Button type="submit" size="icon" aria-label="Guardar nombre" disabled={update.isPending}>
              {update.isPending ? <Loader2 className="animate-spin" /> : <Check />}
            </Button>
            <Button
              type="button"
              variant="ghost"
              size="icon"
              aria-label="Cancelar edición"
              onClick={() => {
                setEditing(false)
                setName(category.name)
                setError(null)
              }}
            >
              <X />
            </Button>
          </div>
          {error && <p className="text-destructive text-sm">{error}</p>}
        </form>
      </li>
    )
  }

  return (
    <li className="hover:bg-muted/60 group flex items-center gap-3 rounded-lg px-2 py-2">
      <span className="min-w-0 flex-1 truncate text-sm font-medium">{category.name}</span>
      <Badge variant="secondary" className="tabular-nums">
        {plural(category.productCount, 'producto', 'productos')}
      </Badge>
      <div className="flex items-center gap-0.5">
        <Button
          variant="ghost"
          size="icon-sm"
          aria-label={`Renombrar ${category.name}`}
          onClick={() => {
            setName(category.name)
            setEditing(true)
          }}
        >
          <Pencil />
        </Button>
        <Button
          variant="ghost"
          size="icon-sm"
          className="text-destructive hover:text-destructive"
          aria-label={`Eliminar ${category.name}`}
          onClick={() => void onDelete()}
        >
          <Trash2 />
        </Button>
      </div>
    </li>
  )
}
