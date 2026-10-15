import { useInfiniteQuery, useQuery } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { Loader2, MessagesSquare, Search, SearchX, Tags, X } from 'lucide-react'
import { memo, useEffect, useMemo, useRef, useState, type KeyboardEvent } from 'react'
import { conversationsQuery, countsQuery, labelsQuery, type InboxConversation } from '../api'
import { useInboxFilters } from '../filters'
import { useDebouncedValue } from '../hooks'
import { categories, channelLabels, channelTypes, contactName, type InboxSearch } from '../meta'
import { ContactAvatar, LabelDot, StatusBadge } from './bits'
import { LabelManagerDialog } from './label-manager-dialog'
import { channelMeta } from '@/components/shared/channel'
import { EmptyState } from '@/components/shared/empty-state'
import { QueryError } from '@/components/shared/query-state'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group'
import { formatChatTimestamp } from '@/lib/format'
import { cn } from '@/lib/utils'

interface ConversationListPanelProps {
  activeId: number | undefined
  /** En pantallas sin columna lateral se muestran aquí la categoría y la etiqueta. */
  compact: boolean
}

/** Lista de conversaciones con búsqueda, filtros y scroll infinito. */
export function ConversationListPanel({ activeId, compact }: ConversationListPanelProps) {
  const { search, filters, setFilters } = useInboxFilters()
  const query = useInfiniteQuery(conversationsQuery(filters))
  const [manageOpen, setManageOpen] = useState(false)

  // Las páginas pueden solaparse cuando llegan mensajes nuevos: se deduplica por id
  const conversations = useMemo(() => {
    const seen = new Set<number>()
    const out: InboxConversation[] = []
    for (const page of query.data?.pages ?? []) {
      for (const c of page.items) {
        if (!seen.has(c.id)) {
          seen.add(c.id)
          out.push(c)
        }
      }
    }
    return out
  }, [query.data])

  const total = query.data?.pages[0]?.totalCount
  const hasFilters = filters.status !== 'All' || !!filters.channel || !!filters.labelId || !!filters.search
  // Cambiar la clave reinicia el buscador cuando los filtros se limpian desde fuera
  const [searchKey, setSearchKey] = useState(0)
  const clearFilters = () => {
    setSearchKey((k) => k + 1)
    setFilters({ status: undefined, channel: undefined, label: undefined, q: undefined })
  }

  return (
    <section aria-label="Conversaciones" className="bg-card flex h-full min-h-0 flex-col">
      <header className="space-y-3 border-b p-3">
        {compact ? (
          <div className="flex items-center gap-2">
            <CategorySelect />
            <LabelFilterMenu onManage={() => setManageOpen(true)} />
          </div>
        ) : (
          <div className="flex items-baseline justify-between gap-2 px-1">
            <h1 className="text-lg font-semibold tracking-tight">Conversaciones</h1>
            {total !== undefined && (
              <span className="text-muted-foreground text-xs tabular-nums">
                {total} {total === 1 ? 'resultado' : 'resultados'}
              </span>
            )}
          </div>
        )}
        <SearchBox
          key={searchKey}
          initial={search.q ?? ''}
          onSearch={(q) => setFilters({ q }, { replace: true })}
        />
        <ToggleGroup
          type="single"
          size="sm"
          variant="outline"
          value={filters.channel ?? 'all'}
          onValueChange={(v) =>
            setFilters({ channel: v && v !== 'all' ? (v as InboxSearch['channel']) : undefined })
          }
          aria-label="Filtrar por canal"
          className="w-full"
        >
          <ToggleGroupItem value="all" className="flex-1 text-xs">
            Todos
          </ToggleGroupItem>
          {channelTypes.map((channel) => {
            const Icon = channelMeta[channel].icon
            return (
              <ToggleGroupItem
                key={channel}
                value={channel}
                aria-label={channelLabels[channel]}
                title={channelLabels[channel]}
                className="flex-1"
              >
                <Icon className="size-4" />
              </ToggleGroupItem>
            )
          })}
        </ToggleGroup>
      </header>

      <div className="min-h-0 flex-1 overflow-y-auto" data-conversation-scroll>
        {query.isPending ? (
          <ListSkeleton />
        ) : query.isError ? (
          <div className="p-3">
            <QueryError error={query.error} onRetry={() => void query.refetch()} />
          </div>
        ) : conversations.length === 0 ? (
          hasFilters ? (
            <EmptyState
              className="m-3 border-0"
              icon={SearchX}
              title="Sin resultados"
              description="Ninguna conversación coincide con los filtros. Prueba con otros criterios."
              action={
                <Button variant="outline" size="sm" onClick={clearFilters}>
                  Limpiar filtros
                </Button>
              }
            />
          ) : (
            <EmptyState
              className="m-3 border-0"
              icon={MessagesSquare}
              title="Aún no tienes conversaciones"
              description="Cuando tus clientes te escriban por WhatsApp, Instagram o Messenger aparecerán aquí."
              action={
                <Button asChild size="sm">
                  <Link to="/channels">Conectar canales</Link>
                </Button>
              }
            />
          )
        ) : (
          <ConversationItems
            conversations={conversations}
            activeId={activeId}
            search={search}
            hasNextPage={query.hasNextPage}
            isFetchingNextPage={query.isFetchingNextPage}
            fetchNextPage={() => void query.fetchNextPage()}
          />
        )}
      </div>

      <LabelManagerDialog open={manageOpen} onOpenChange={setManageOpen} />
    </section>
  )
}

function SearchBox({ initial, onSearch }: { initial: string; onSearch: (q: string | undefined) => void }) {
  const [value, setValue] = useState(initial)
  const debounced = useDebouncedValue(value, 300)
  const onSearchRef = useRef(onSearch)
  useEffect(() => {
    onSearchRef.current = onSearch
  })
  const lastSent = useRef(initial.trim())
  useEffect(() => {
    const term = debounced.trim()
    if (term === lastSent.current) return
    lastSent.current = term
    onSearchRef.current(term || undefined)
  }, [debounced])

  return (
    <div className="relative">
      <Search
        className="text-muted-foreground pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2"
        aria-hidden
      />
      <Input
        type="search"
        value={value}
        onChange={(e) => setValue(e.target.value)}
        onKeyDown={(e) => {
          if (e.key === 'Escape' && value) {
            e.preventDefault()
            setValue('')
          }
        }}
        placeholder="Buscar por nombre, teléfono o mensaje…"
        aria-label="Buscar conversaciones"
        maxLength={100}
        className="h-9 pr-9 pl-9 [&::-webkit-search-cancel-button]:hidden"
      />
      {value && (
        <Button
          variant="ghost"
          size="icon-xs"
          aria-label="Limpiar búsqueda"
          className="absolute top-1/2 right-2 -translate-y-1/2"
          onClick={() => setValue('')}
        >
          <X />
        </Button>
      )}
    </div>
  )
}

function CategorySelect() {
  const { filters, setFilters } = useInboxFilters()
  const counts = useQuery(countsQuery({ channel: filters.channel, labelId: filters.labelId }))
  return (
    <Select value={filters.status} onValueChange={(v) => setFilters({ status: v as InboxSearch['status'] })}>
      <SelectTrigger className="h-9 min-w-0 flex-1" aria-label="Categoría">
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        {categories.map((c) => (
          <SelectItem key={c.value} value={c.value}>
            <c.icon aria-hidden />
            {c.label}
            {counts.data && (
              <span className="text-muted-foreground ml-1 text-xs tabular-nums">{counts.data[c.count]}</span>
            )}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}

function LabelFilterMenu({ onManage }: { onManage: () => void }) {
  const { filters, setFilters } = useInboxFilters()
  const labels = useQuery(labelsQuery)
  const active = labels.data?.find((l) => l.id === filters.labelId)
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="outline"
          size={active ? 'sm' : 'icon'}
          className={cn('relative', active ? 'h-9 max-w-36' : 'size-9')}
          aria-label={active ? `Etiqueta: ${active.name}` : 'Filtrar por etiqueta'}
        >
          {active ? (
            <>
              <LabelDot color={active.color} />
              <span className="truncate">{active.name}</span>
            </>
          ) : (
            <Tags />
          )}
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-56">
        <DropdownMenuLabel>Filtrar por etiqueta</DropdownMenuLabel>
        <DropdownMenuRadioGroup
          value={filters.labelId ? String(filters.labelId) : 'none'}
          onValueChange={(v) => setFilters({ label: v === 'none' ? undefined : Number(v) })}
        >
          <DropdownMenuRadioItem value="none">Todas las etiquetas</DropdownMenuRadioItem>
          {labels.data?.map((label) => (
            <DropdownMenuRadioItem key={label.id} value={String(label.id)}>
              <LabelDot color={label.color} />
              <span className="truncate">{label.name}</span>
            </DropdownMenuRadioItem>
          ))}
        </DropdownMenuRadioGroup>
        <DropdownMenuSeparator />
        <DropdownMenuItem onSelect={onManage}>
          <Tags /> Gestionar etiquetas…
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

function ConversationItems({
  conversations,
  activeId,
  search,
  hasNextPage,
  isFetchingNextPage,
  fetchNextPage,
}: {
  conversations: InboxConversation[]
  activeId: number | undefined
  search: InboxSearch
  hasNextPage: boolean
  isFetchingNextPage: boolean
  fetchNextPage: () => void
}) {
  const sentinel = useRef<HTMLDivElement>(null)
  const fetchRef = useRef(fetchNextPage)
  useEffect(() => {
    fetchRef.current = fetchNextPage
  })

  useEffect(() => {
    const el = sentinel.current
    if (!el || !hasNextPage) return
    const io = new IntersectionObserver(
      ([entry]) => {
        if (entry?.isIntersecting && !isFetchingNextPage) fetchRef.current()
      },
      { root: el.closest('[data-conversation-scroll]'), rootMargin: '0px 0px 300px 0px' },
    )
    io.observe(el)
    return () => io.disconnect()
  }, [hasNextPage, isFetchingNextPage])

  // Flechas arriba/abajo para moverse entre conversaciones
  const onKeyDown = (e: KeyboardEvent<HTMLUListElement>) => {
    if (e.key !== 'ArrowDown' && e.key !== 'ArrowUp') return
    const items = Array.from(e.currentTarget.querySelectorAll<HTMLElement>('[data-conversation-item]'))
    const index = items.indexOf(document.activeElement as HTMLElement)
    if (index === -1) return
    e.preventDefault()
    items[e.key === 'ArrowDown' ? Math.min(index + 1, items.length - 1) : Math.max(index - 1, 0)]?.focus()
  }

  return (
    <>
      <ul className="space-y-0.5 p-1.5" onKeyDown={onKeyDown}>
        {conversations.map((c) => (
          <li key={c.id}>
            <ConversationItem conversation={c} active={c.id === activeId} search={search} />
          </li>
        ))}
      </ul>
      <div ref={sentinel} aria-hidden className="h-px" />
      {isFetchingNextPage && (
        <div
          className="text-muted-foreground flex items-center justify-center gap-2 py-3 text-xs"
          role="status"
        >
          <Loader2 className="size-4 animate-spin" aria-hidden /> Cargando más…
        </div>
      )}
    </>
  )
}

const ConversationItem = memo(function ConversationItem({
  conversation: c,
  active,
  search,
}: {
  conversation: InboxConversation
  active: boolean
  search: InboxSearch
}) {
  const name = contactName(c.contact, c.channel)
  const unread = c.unreadCount > 0
  return (
    <Link
      to="/inbox/$conversationId"
      params={{ conversationId: c.id }}
      search={search}
      data-conversation-item
      aria-current={active ? 'page' : undefined}
      className={cn(
        'focus-visible:ring-ring/50 flex gap-3 rounded-lg px-2.5 py-2.5 transition-colors outline-none focus-visible:ring-[3px]',
        active ? 'bg-primary/10' : 'hover:bg-muted/70',
      )}
    >
      <ContactAvatar name={name} avatarUrl={c.contact.avatarUrl} channel={c.channel} />
      <div className="min-w-0 flex-1">
        <div className="flex items-baseline gap-2">
          <span className={cn('truncate text-sm', unread ? 'font-semibold' : 'font-medium')}>{name}</span>
          {c.lastMessageAt && (
            <time
              dateTime={c.lastMessageAt}
              className={cn(
                'ml-auto shrink-0 text-xs',
                unread ? 'text-primary font-medium' : 'text-muted-foreground',
              )}
            >
              {formatChatTimestamp(c.lastMessageAt)}
            </time>
          )}
        </div>
        <div className="mt-0.5 flex items-center gap-2">
          <p
            className={cn(
              'min-w-0 flex-1 truncate text-sm',
              unread ? 'text-foreground' : 'text-muted-foreground',
            )}
          >
            {c.lastMessagePreview || <span className="italic">Sin mensajes</span>}
          </p>
          {unread && (
            <span className="bg-primary text-primary-foreground flex h-5 min-w-5 shrink-0 items-center justify-center rounded-full px-1.5 text-[11px] font-semibold tabular-nums">
              {c.unreadCount > 99 ? '99+' : c.unreadCount}
              <span className="sr-only"> mensajes sin leer</span>
            </span>
          )}
        </div>
        {(c.status !== 'BotActive' || c.label) && (
          <div className="mt-1.5 flex min-w-0 items-center gap-2">
            {c.status !== 'BotActive' && <StatusBadge status={c.status} className="px-1.5 text-[11px]" />}
            {c.label && (
              <span className="text-muted-foreground inline-flex min-w-0 items-center gap-1.5 text-xs">
                <LabelDot color={c.label.color} />
                <span className="truncate">{c.label.name}</span>
              </span>
            )}
          </div>
        )}
      </div>
    </Link>
  )
})

function ListSkeleton() {
  return (
    <div className="space-y-1 p-1.5" role="status" aria-label="Cargando conversaciones">
      {Array.from({ length: 8 }, (_, i) => (
        <div key={i} className="flex gap-3 px-2.5 py-2.5">
          <Skeleton className="size-10 shrink-0 rounded-full" />
          <div className="flex-1 space-y-2 py-0.5">
            <div className="flex justify-between gap-4">
              <Skeleton className="h-4 w-1/2" />
              <Skeleton className="h-3 w-10" />
            </div>
            <Skeleton className="h-3.5 w-4/5" />
          </div>
        </div>
      ))}
    </div>
  )
}
