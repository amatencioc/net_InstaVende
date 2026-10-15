import { useQuery } from '@tanstack/react-query'
import {
  Ban,
  BellPlus,
  BellRing,
  ChevronLeft,
  ChevronRight,
  MoreHorizontal,
  Pencil,
  Search,
  SearchX,
  Trash2,
  X,
} from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { Alert } from '@/components/shared/alert'
import { EmptyState } from '@/components/shared/empty-state'
import { QueryError } from '@/components/shared/query-state'
import { useConfirm } from '@/components/shared/confirm-dialog'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { formatDateTime, formatNumber, formatRelative } from '@/lib/format'
import { cn } from '@/lib/utils'
import {
  remindersQueries,
  useCancelReminder,
  useDeleteReminder,
  type Reminder,
  type ReminderCounts,
  type ReminderStatus,
} from '../api'
import { reminderStatuses, statusMeta } from '../constants'
import { useTimeZoneMismatch } from '../hooks'
import { contactLabel } from '../utils'
import { ContactAvatar } from './contact-avatar'
import { ReminderDialog } from './reminder-dialog'

export type StatusFilter = ReminderStatus | 'all'

export interface ScheduledRemindersSearch {
  status: StatusFilter
  q: string
  page: number
}

interface ScheduledRemindersProps {
  search: ScheduledRemindersSearch
  onSearchChange: (next: Partial<ScheduledRemindersSearch>) => void
}

/** Listado de recordatorios programados con filtros por estado, búsqueda y acciones. */
export function ScheduledReminders({ search, onSearchChange }: ScheduledRemindersProps) {
  const [term, setTerm] = useState(search.q)
  // Si la búsqueda de la URL cambia desde fuera (navegación, atrás), se sincroniza el campo.
  const [syncedQ, setSyncedQ] = useState(search.q)
  if (search.q !== syncedQ) {
    setSyncedQ(search.q)
    if (search.q !== term.trim()) setTerm(search.q)
  }
  const searchTimer = useRef<number | undefined>(undefined)
  useEffect(() => () => window.clearTimeout(searchTimer.current), [])

  // La búsqueda se aplica a la URL cuando el usuario deja de escribir (debounce).
  const updateTerm = (value: string) => {
    setTerm(value)
    window.clearTimeout(searchTimer.current)
    searchTimer.current = window.setTimeout(() => onSearchChange({ q: value.trim(), page: 1 }), 350)
  }

  const [dialog, setDialog] = useState<{ open: boolean; reminder: Reminder | null }>({
    open: false,
    reminder: null,
  })
  const tzMismatch = useTimeZoneMismatch()

  const status = search.status === 'all' ? undefined : search.status
  const list = useQuery(remindersQueries.list({ status, search: search.q, page: search.page }))
  const counts = useQuery(remindersQueries.counts(search.q))

  const openCreate = () => setDialog({ open: true, reminder: null })
  const openEdit = (reminder: Reminder) => setDialog({ open: true, reminder })

  return (
    <div className="space-y-4">
      <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <StatusTabs
          value={search.status}
          counts={counts.data}
          onChange={(s) => onSearchChange({ status: s, page: 1 })}
        />
        <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
          <div className="relative sm:w-72">
            <Search
              className="text-muted-foreground pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2"
              aria-hidden
            />
            <Input
              type="search"
              value={term}
              onChange={(e) => updateTerm(e.target.value)}
              placeholder="Buscar contacto o mensaje…"
              aria-label="Buscar recordatorios"
              className="pr-9 pl-9"
            />
            {term && (
              <Button
                type="button"
                variant="ghost"
                size="icon-xs"
                className="absolute top-1/2 right-2 -translate-y-1/2"
                onClick={() => updateTerm('')}
                aria-label="Limpiar búsqueda"
              >
                <X />
              </Button>
            )}
          </div>
          <Button onClick={openCreate}>
            <BellPlus aria-hidden /> Nuevo recordatorio
          </Button>
        </div>
      </div>

      {tzMismatch && (
        <Alert tone="info">
          Las fechas se muestran en la zona horaria de tu dispositivo ({tzMismatch.browser}). Tu negocio usa{' '}
          {tzMismatch.business}.
        </Alert>
      )}

      {list.isError ? (
        <QueryError error={list.error} onRetry={() => void list.refetch()} />
      ) : list.isPending ? (
        <RemindersSkeleton />
      ) : list.data.items.length === 0 && list.data.totalCount > 0 ? (
        // Página fuera de rango (p.ej. tras eliminar el último elemento de la última página)
        <EmptyState
          icon={SearchX}
          title="Esta página ya no tiene recordatorios"
          action={
            <Button variant="outline" onClick={() => onSearchChange({ page: 1 })}>
              Ir a la primera página
            </Button>
          }
        />
      ) : list.data.items.length === 0 ? (
        <RemindersEmpty
          status={search.status}
          searching={!!search.q}
          onCreate={openCreate}
          onClear={() => updateTerm('')}
        />
      ) : (
        <div
          className={cn('space-y-3 transition-opacity', list.isPlaceholderData && 'opacity-60')}
          aria-busy={list.isFetching}
        >
          <RemindersTable items={list.data.items} onEdit={openEdit} />
          <RemindersCards items={list.data.items} onEdit={openEdit} />
          <PaginationBar
            page={list.data.page}
            totalPages={list.data.totalPages ?? 1}
            totalCount={list.data.totalCount}
            onPageChange={(page) => onSearchChange({ page })}
          />
        </div>
      )}

      <ReminderDialog
        open={dialog.open}
        reminder={dialog.reminder}
        onOpenChange={(open) => setDialog((d) => ({ ...d, open }))}
        onSaved={(created) => {
          // Un recordatorio nuevo siempre está pendiente: se muestra en su pestaña.
          if (created && search.status !== 'Pending' && search.status !== 'all')
            onSearchChange({ status: 'Pending', page: 1 })
        }}
      />
    </div>
  )
}

// ── Pestañas de estado ──────────────────────────────────────────────────────

function StatusTabs({
  value,
  counts,
  onChange,
}: {
  value: StatusFilter
  counts?: ReminderCounts
  onChange: (status: StatusFilter) => void
}) {
  const total = counts ? counts.pending + counts.sent + counts.failed + counts.cancelled : undefined
  const tabs: { value: StatusFilter; label: string; count?: number }[] = [
    ...reminderStatuses.map((s) => ({
      value: s,
      label: statusMeta[s].tabLabel,
      count: counts?.[statusMeta[s].countKey],
    })),
    { value: 'all', label: 'Todos', count: total },
  ]

  return (
    <Tabs
      value={value}
      onValueChange={(v) => onChange(v as StatusFilter)}
      className="-mx-1 overflow-x-auto px-1"
    >
      <TabsList aria-label="Filtrar por estado" className="h-auto min-w-max">
        {tabs.map((t) => (
          <TabsTrigger key={t.value} value={t.value} className="gap-2 px-3 py-1.5">
            {t.label}
            <span
              className={cn(
                'bg-muted-foreground/10 text-muted-foreground min-w-5 rounded-full px-1.5 text-[11px] leading-5 tabular-nums',
                t.value === 'Failed' && t.count ? 'bg-destructive/10 text-destructive' : '',
              )}
            >
              {t.count === undefined ? '–' : formatNumber(t.count)}
            </span>
          </TabsTrigger>
        ))}
      </TabsList>
    </Tabs>
  )
}

// ── Listado ─────────────────────────────────────────────────────────────────

export function StatusBadge({ status }: { status: ReminderStatus }) {
  const meta = statusMeta[status]
  const Icon = meta.icon
  return (
    <Badge variant="outline" className={cn('gap-1', meta.className)}>
      <Icon aria-hidden /> {meta.label}
    </Badge>
  )
}

function ScheduleCell({ reminder }: { reminder: Reminder }) {
  return (
    <div className="space-y-0.5">
      <time dateTime={reminder.scheduledAt} className="block text-sm whitespace-nowrap tabular-nums">
        {formatDateTime(reminder.scheduledAt)}
      </time>
      <span className="text-muted-foreground block text-xs">
        {reminder.status === 'Pending'
          ? formatRelative(reminder.scheduledAt)
          : reminder.sentAt
            ? `Procesado ${formatRelative(reminder.sentAt)}`
            : null}
      </span>
    </div>
  )
}

function RemindersTable({ items, onEdit }: { items: Reminder[]; onEdit: (r: Reminder) => void }) {
  return (
    <Card className="hidden overflow-hidden py-0 md:block">
      <Table>
        <TableHeader>
          <TableRow className="hover:bg-transparent">
            <TableHead className="pl-4">Contacto</TableHead>
            <TableHead>Mensaje</TableHead>
            <TableHead>Programado</TableHead>
            <TableHead>Estado</TableHead>
            <TableHead className="hidden xl:table-cell">Creado por</TableHead>
            <TableHead className="w-12 pr-4">
              <span className="sr-only">Acciones</span>
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {items.map((r) => {
            const label = contactLabel({ name: r.contactName, phone: r.contactPhone, id: r.contactId })
            return (
              <TableRow key={r.id}>
                <TableCell className="pl-4">
                  <div className="flex items-center gap-3">
                    <ContactAvatar name={label} avatarUrl={r.contactAvatarUrl} channel={r.channel} />
                    <div className="min-w-0">
                      <p className="max-w-44 truncate font-medium">{label}</p>
                      {r.contactPhone && r.contactName && (
                        <p className="text-muted-foreground text-xs tabular-nums">{r.contactPhone}</p>
                      )}
                    </div>
                  </div>
                </TableCell>
                <TableCell className="max-w-md whitespace-normal">
                  <p className="text-muted-foreground line-clamp-2 text-sm" title={r.message}>
                    {r.message}
                  </p>
                </TableCell>
                <TableCell>
                  <ScheduleCell reminder={r} />
                </TableCell>
                <TableCell>
                  <StatusBadge status={r.status} />
                </TableCell>
                <TableCell className="text-muted-foreground hidden text-sm xl:table-cell">
                  {r.createdByName || 'Sistema'}
                </TableCell>
                <TableCell className="pr-4 text-right">
                  <ReminderActions reminder={r} onEdit={onEdit} />
                </TableCell>
              </TableRow>
            )
          })}
        </TableBody>
      </Table>
    </Card>
  )
}

function RemindersCards({ items, onEdit }: { items: Reminder[]; onEdit: (r: Reminder) => void }) {
  return (
    <ul className="space-y-3 md:hidden">
      {items.map((r) => {
        const label = contactLabel({ name: r.contactName, phone: r.contactPhone, id: r.contactId })
        return (
          <li key={r.id}>
            <Card className="gap-3 p-4">
              <div className="flex items-start justify-between gap-3">
                <div className="flex min-w-0 items-center gap-3">
                  <ContactAvatar name={label} avatarUrl={r.contactAvatarUrl} channel={r.channel} />
                  <div className="min-w-0">
                    <p className="truncate font-medium">{label}</p>
                    <StatusBadge status={r.status} />
                  </div>
                </div>
                <ReminderActions reminder={r} onEdit={onEdit} />
              </div>
              <p className="text-muted-foreground line-clamp-3 text-sm">{r.message}</p>
              <div className="flex items-end justify-between gap-2">
                <ScheduleCell reminder={r} />
                <span className="text-muted-foreground text-xs">{r.createdByName || 'Sistema'}</span>
              </div>
            </Card>
          </li>
        )
      })}
    </ul>
  )
}

function ReminderActions({ reminder, onEdit }: { reminder: Reminder; onEdit: (r: Reminder) => void }) {
  const confirm = useConfirm()
  const cancel = useCancelReminder()
  const remove = useDeleteReminder()
  const pending = reminder.status === 'Pending'
  const canDelete = reminder.status !== 'Sent'
  const label = contactLabel({
    name: reminder.contactName,
    phone: reminder.contactPhone,
    id: reminder.contactId,
  })

  if (!pending && !canDelete) return <span className="inline-block size-8" aria-hidden />

  const onCancel = async () => {
    const ok = await confirm({
      title: 'Cancelar recordatorio',
      description: `El mensaje programado para ${label} no se enviará. Podrás verlo en la pestaña Cancelados.`,
      confirmLabel: 'Cancelar recordatorio',
      cancelLabel: 'Volver',
    })
    if (ok) cancel.mutate(reminder.id)
  }

  const onDelete = async () => {
    const ok = await confirm({
      title: 'Eliminar recordatorio',
      description: 'Se eliminará de forma permanente. Esta acción no se puede deshacer.',
      confirmLabel: 'Eliminar',
      tone: 'destructive',
    })
    if (ok) remove.mutate(reminder.id)
  }

  return (
    // modal={false}: evita que el menú bloquee el puntero al abrir diálogos desde sus opciones.
    <DropdownMenu modal={false}>
      <DropdownMenuTrigger asChild>
        <Button
          variant="ghost"
          size="icon-sm"
          aria-label={`Acciones del recordatorio para ${label}`}
          disabled={cancel.isPending || remove.isPending}
        >
          <MoreHorizontal />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        {pending && (
          <>
            <DropdownMenuItem onSelect={() => onEdit(reminder)}>
              <Pencil aria-hidden /> Editar
            </DropdownMenuItem>
            <DropdownMenuItem onSelect={() => void onCancel()}>
              <Ban aria-hidden /> Cancelar envío
            </DropdownMenuItem>
          </>
        )}
        {pending && canDelete && <DropdownMenuSeparator />}
        {canDelete && (
          <DropdownMenuItem variant="destructive" onSelect={() => void onDelete()}>
            <Trash2 aria-hidden /> Eliminar
          </DropdownMenuItem>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

// ── Estados auxiliares ──────────────────────────────────────────────────────

function RemindersSkeleton() {
  return (
    <Card className="gap-0 py-0" aria-busy="true" aria-label="Cargando recordatorios">
      {Array.from({ length: 5 }, (_, i) => (
        <div key={i} className="flex items-center gap-4 border-b p-4 last:border-b-0">
          <Skeleton className="size-8 rounded-full" />
          <div className="flex-1 space-y-2">
            <Skeleton className="h-4 w-40" />
            <Skeleton className="h-3 w-full max-w-md" />
          </div>
          <Skeleton className="hidden h-4 w-28 sm:block" />
          <Skeleton className="h-5 w-20 rounded-full" />
        </div>
      ))}
    </Card>
  )
}

function RemindersEmpty({
  status,
  searching,
  onCreate,
  onClear,
}: {
  status: StatusFilter
  searching: boolean
  onCreate: () => void
  onClear: () => void
}) {
  if (searching)
    return (
      <EmptyState
        icon={SearchX}
        title="Sin resultados"
        description="No hay recordatorios que coincidan con tu búsqueda."
        action={
          <Button variant="outline" onClick={onClear}>
            Limpiar búsqueda
          </Button>
        }
      />
    )

  const copy: Record<StatusFilter, { title: string; description: string }> = {
    Pending: {
      title: 'No tienes recordatorios pendientes',
      description: 'Programa un mensaje de seguimiento y lo enviaremos por ti en el momento justo.',
    },
    Sent: {
      title: 'Aún no se ha enviado ningún recordatorio',
      description: 'Aquí verás el historial de mensajes enviados.',
    },
    Failed: {
      title: 'Sin envíos fallidos',
      description: 'Todos tus recordatorios se enviaron correctamente.',
    },
    Cancelled: {
      title: 'Sin recordatorios cancelados',
      description: 'Los recordatorios que canceles aparecerán aquí.',
    },
    all: {
      title: 'Aún no tienes recordatorios',
      description: 'Programa mensajes de seguimiento para retomar conversaciones y cerrar más ventas.',
    },
  }
  const { title, description } = copy[status]
  const showCreate = status === 'Pending' || status === 'all'

  return (
    <EmptyState
      icon={status === 'all' ? BellRing : statusMeta[status].icon}
      title={title}
      description={description}
      action={
        showCreate ? (
          <Button onClick={onCreate}>
            <BellPlus aria-hidden /> Programar recordatorio
          </Button>
        ) : undefined
      }
    />
  )
}

function PaginationBar({
  page,
  totalPages,
  totalCount,
  onPageChange,
}: {
  page: number
  totalPages: number
  totalCount: number
  onPageChange: (page: number) => void
}) {
  if (totalPages <= 1)
    return (
      <p className="text-muted-foreground text-sm">
        {formatNumber(totalCount)} {totalCount === 1 ? 'recordatorio' : 'recordatorios'}
      </p>
    )
  return (
    <nav aria-label="Paginación" className="flex items-center justify-between gap-3">
      <p className="text-muted-foreground text-sm">
        Página {page} de {totalPages} · {formatNumber(totalCount)} recordatorios
      </p>
      <div className="flex gap-2">
        <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => onPageChange(page - 1)}>
          <ChevronLeft aria-hidden /> Anterior
        </Button>
        <Button
          variant="outline"
          size="sm"
          disabled={page >= totalPages}
          onClick={() => onPageChange(page + 1)}
        >
          Siguiente <ChevronRight aria-hidden />
        </Button>
      </div>
    </nav>
  )
}
