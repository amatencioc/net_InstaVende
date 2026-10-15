import { useQuery } from '@tanstack/react-query'
import { BookOpen, Lightbulb, Plus, Search, SearchX, Upload, X } from 'lucide-react'
import { useState } from 'react'
import {
  knowledgeListQuery,
  useDeleteKnowledge,
  useToggleKnowledgeFavorite,
  type KnowledgeCategory,
  type KnowledgeEntry,
  type KnowledgeList,
} from '../../api'
import { allCategoriesMeta, categoryMeta, knowledgeCategories, knowledgeSuggestions } from '../../meta'
import { useDebouncedValue } from '../../utils'
import { SectionHeader } from '../section-header'
import { KnowledgeCard } from './knowledge-card'
import { KnowledgeDialog, type KnowledgeDialogState } from './knowledge-dialog'
import { KnowledgeImportDialog } from './knowledge-import-dialog'
import { useConfirm } from '@/components/shared/confirm-dialog'
import { EmptyState } from '@/components/shared/empty-state'
import { QueryError } from '@/components/shared/query-state'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { cn } from '@/lib/utils'

type CategoryFilter = KnowledgeCategory | 'all'

/** Pestaña "Base de conocimiento": temas que el vendedor IA usa para responder. */
export function KnowledgePage() {
  const [category, setCategory] = useState<CategoryFilter>('all')
  const [search, setSearch] = useState('')
  const debouncedSearch = useDebouncedValue(search.trim(), 300)
  const [dialog, setDialog] = useState<KnowledgeDialogState | null>(null)
  const [importOpen, setImportOpen] = useState(false)

  const query = useQuery(
    knowledgeListQuery({ category: category === 'all' ? undefined : category, search: debouncedSearch || undefined }),
  )
  const toggleFavorite = useToggleKnowledgeFavorite()
  const remove = useDeleteKnowledge()
  const confirm = useConfirm()

  const openCreate = (preset?: KnowledgeDialogState['preset']) =>
    setDialog({ preset: preset ?? (category === 'all' ? undefined : { category }) })

  const handleDelete = async (entry: KnowledgeEntry) => {
    const ok = await confirm({
      title: 'Eliminar tema',
      description: `Tu vendedor dejará de usar "${entry.title}" para responder. Esta acción no se puede deshacer.`,
      confirmLabel: 'Eliminar',
      tone: 'destructive',
    })
    if (ok) remove.mutate(entry.id)
  }

  const clearFilters = () => {
    setSearch('')
    setCategory('all')
  }

  const data = query.data
  const isEmptyBase = data !== undefined && data.total === 0 && !debouncedSearch

  return (
    <section aria-labelledby="knowledge-title" className="flex flex-col gap-5">
      <SectionHeader
        id="knowledge-title"
        icon={BookOpen}
        title="Base de conocimiento"
        description="Todo lo que tu vendedor IA necesita saber para responder: horarios, envíos, políticas, garantías y más."
        actions={
          <>
            <Button variant="outline" onClick={() => setImportOpen(true)}>
              <Upload aria-hidden /> Importar CSV
            </Button>
            <Button onClick={() => openCreate()}>
              <Plus aria-hidden /> Agregar tema
            </Button>
          </>
        }
      />

      {query.isError && !data ? (
        <QueryError error={query.error} onRetry={() => void query.refetch()} />
      ) : isEmptyBase ? (
        <KnowledgeEmpty onCreate={openCreate} onImport={() => setImportOpen(true)} />
      ) : (
        <>
          <div className="relative w-full sm:max-w-md">
            <Search
              className="text-muted-foreground pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2"
              aria-hidden
            />
            <Input
              type="search"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Buscar por pregunta o respuesta…"
              aria-label="Buscar temas"
              className="pr-9 pl-9 [&::-webkit-search-cancel-button]:hidden"
              maxLength={100}
            />
            {search && (
              <Button
                variant="ghost"
                size="icon-xs"
                className="absolute top-1/2 right-2 -translate-y-1/2"
                onClick={() => setSearch('')}
                aria-label="Limpiar búsqueda"
              >
                <X aria-hidden />
              </Button>
            )}
          </div>

          <Tabs value={category} onValueChange={(v) => setCategory(v as CategoryFilter)}>
            <div className="-mx-1 overflow-x-auto px-1 pb-1">
              <TabsList className="h-auto min-w-max">
                <CategoryTab value="all" label={allCategoriesMeta.label} icon={allCategoriesMeta.icon} count={data?.total} />
                {knowledgeCategories.map((c) => (
                  <CategoryTab
                    key={c}
                    value={c}
                    label={categoryMeta[c].label}
                    icon={categoryMeta[c].icon}
                    count={data ? countFor(data, c) : undefined}
                  />
                ))}
              </TabsList>
            </div>
            {(['all', ...knowledgeCategories] as CategoryFilter[]).map((value) => (
              <TabsContent key={value} value={value} className="mt-2">
                <KnowledgeResults
                  data={data}
                  loading={query.isPending}
                  refreshing={query.isPlaceholderData}
                  category={category}
                  search={debouncedSearch}
                  onCreate={openCreate}
                  onClear={clearFilters}
                  onEdit={(entry) => setDialog({ entry })}
                  onDelete={(entry) => void handleDelete(entry)}
                  onToggleFavorite={(entry) => toggleFavorite.mutate({ id: entry.id, isFavorite: !entry.isFavorite })}
                />
              </TabsContent>
            ))}
          </Tabs>
        </>
      )}

      <KnowledgeDialog state={dialog} onOpenChange={(open) => !open && setDialog(null)} />
      <KnowledgeImportDialog open={importOpen} onOpenChange={setImportOpen} />
    </section>
  )
}

function countFor(data: KnowledgeList, category: KnowledgeCategory): number {
  return data.counts.find((c) => c.category === category)?.count ?? 0
}

function CategoryTab({
  value,
  label,
  icon: Icon,
  count,
}: {
  value: CategoryFilter
  label: string
  icon: typeof BookOpen
  count: number | undefined
}) {
  return (
    <TabsTrigger value={value} className="gap-2 px-3 py-1.5">
      <Icon aria-hidden />
      {label}
      {count !== undefined && (
        <span
          className="bg-foreground/5 text-muted-foreground min-w-5 rounded-full px-1.5 text-xs tabular-nums"
          aria-label={`${count} temas`}
        >
          {count}
        </span>
      )}
    </TabsTrigger>
  )
}

interface KnowledgeResultsProps {
  data: KnowledgeList | undefined
  loading: boolean
  refreshing: boolean
  category: CategoryFilter
  search: string
  onCreate: (preset?: KnowledgeDialogState['preset']) => void
  onClear: () => void
  onEdit: (entry: KnowledgeEntry) => void
  onDelete: (entry: KnowledgeEntry) => void
  onToggleFavorite: (entry: KnowledgeEntry) => void
}

function KnowledgeResults({
  data,
  loading,
  refreshing,
  category,
  search,
  onCreate,
  onClear,
  onEdit,
  onDelete,
  onToggleFavorite,
}: KnowledgeResultsProps) {
  if (loading || !data) {
    return (
      <div className="grid gap-4 md:grid-cols-2" aria-busy="true" aria-label="Cargando temas">
        {Array.from({ length: 4 }, (_, i) => (
          <Card key={i} className="gap-3 px-4 py-4">
            <Skeleton className="h-5 w-24 rounded-full" />
            <Skeleton className="h-5 w-3/4" />
            <Skeleton className="h-4 w-full" />
            <Skeleton className="h-4 w-5/6" />
          </Card>
        ))}
      </div>
    )
  }

  if (data.items.length === 0) {
    if (search) {
      return (
        <EmptyState
          icon={SearchX}
          title="Sin resultados"
          description={`No encontramos temas que coincidan con "${search}".`}
          action={
            <Button variant="outline" onClick={onClear}>
              Limpiar filtros
            </Button>
          }
        />
      )
    }
    const meta = category === 'all' ? null : categoryMeta[category]
    return (
      <EmptyState
        icon={meta?.icon ?? BookOpen}
        title={meta ? `Aún no hay temas de ${meta.label}` : 'Aún no hay temas'}
        description="Agrega la información que tus clientes suelen preguntar en esta categoría."
        action={
          <Button onClick={() => onCreate(category === 'all' ? undefined : { category })}>
            <Plus aria-hidden /> Agregar tema
          </Button>
        }
      />
    )
  }

  return (
    <ul
      className={cn('grid gap-4 transition-opacity md:grid-cols-2', refreshing && 'opacity-60')}
      aria-busy={refreshing}
      aria-label="Temas de la base de conocimiento"
    >
      {data.items.map((entry) => (
        <li key={entry.id}>
          <KnowledgeCard
            entry={entry}
            onEdit={() => onEdit(entry)}
            onDelete={() => onDelete(entry)}
            onToggleFavorite={() => onToggleFavorite(entry)}
          />
        </li>
      ))}
    </ul>
  )
}

function KnowledgeEmpty({
  onCreate,
  onImport,
}: {
  onCreate: (preset?: KnowledgeDialogState['preset']) => void
  onImport: () => void
}) {
  return (
    <div className="flex flex-col gap-6 rounded-xl border border-dashed px-6 py-10">
      <div className="mx-auto flex max-w-lg flex-col items-center gap-3 text-center">
        <div className="bg-primary/10 text-primary flex size-12 items-center justify-center rounded-full">
          <BookOpen className="size-6" aria-hidden />
        </div>
        <h3 className="font-semibold">Enséñale a tu vendedor sobre tu negocio</h3>
        <p className="text-muted-foreground text-sm">
          Mientras más información agregues, mejor responderá y menos consultas tendrás que atender tú. Empieza por
          las preguntas que más te hacen.
        </p>
        <div className="mt-1 flex flex-wrap justify-center gap-2">
          <Button onClick={() => onCreate()}>
            <Plus aria-hidden /> Agregar tema
          </Button>
          <Button variant="outline" onClick={onImport}>
            <Upload aria-hidden /> Importar CSV
          </Button>
        </div>
      </div>

      <div className="mx-auto w-full max-w-3xl">
        <p className="text-muted-foreground mb-3 flex items-center gap-2 text-sm font-medium">
          <Lightbulb className="text-warning size-4" aria-hidden /> Temas que conviene cubrir
        </p>
        <ul className="grid gap-2 sm:grid-cols-2">
          {knowledgeSuggestions.map((s) => {
            const meta = categoryMeta[s.category]
            return (
              <li key={s.title}>
                <button
                  type="button"
                  onClick={() => onCreate({ title: s.title, category: s.category })}
                  className="hover:border-primary/50 hover:bg-primary/5 focus-visible:ring-ring/50 bg-card flex w-full items-center gap-3 rounded-lg border p-3 text-left text-sm transition-colors outline-none focus-visible:ring-[3px]"
                >
                  <span className={cn('flex size-8 shrink-0 items-center justify-center rounded-md', meta.tone)}>
                    <meta.icon className="size-4" aria-hidden />
                  </span>
                  <span className="min-w-0 flex-1">{s.title}</span>
                  <Plus className="text-muted-foreground size-4 shrink-0" aria-hidden />
                </button>
              </li>
            )
          })}
        </ul>
      </div>
    </div>
  )
}
