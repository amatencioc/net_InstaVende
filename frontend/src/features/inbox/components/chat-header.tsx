import { useQuery } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import {
  ArrowLeft,
  Bot,
  CircleCheck,
  EllipsisVertical,
  Headset,
  Loader2,
  PanelRight,
  RotateCcw,
  Tag,
  Tags,
  type LucideIcon,
} from 'lucide-react'
import { useState } from 'react'
import {
  labelsQuery,
  useChangeStatus,
  useSetConversationLabel,
  type ConversationStatus,
  type InboxConversationDetail,
} from '../api'
import { inboxRouteApi } from '../filters'
import { agentName, channelLabels, contactName, contactPhone } from '../meta'
import { ContactAvatar, LabelDot, StatusBadge } from './bits'
import { LabelManagerDialog } from './label-manager-dialog'
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
import { Skeleton } from '@/components/ui/skeleton'
import { useSession } from '@/lib/auth/session'
import { cn } from '@/lib/utils'

interface StatusAction {
  label: string
  icon: LucideIcon
  status: ConversationStatus
}

const take: StatusAction = { label: 'Tomar control', icon: Headset, status: 'HumanActive' }
const toBot: StatusAction = { label: 'Devolver al bot', icon: Bot, status: 'BotActive' }
const resolve: StatusAction = { label: 'Resolver', icon: CircleCheck, status: 'Resolved' }
const reopen: StatusAction = { label: 'Reabrir', icon: RotateCcw, status: 'BotActive' }
const reopenAndTake: StatusAction = { label: 'Reabrir y atender', icon: Headset, status: 'HumanActive' }

/** Acción principal y secundarias según el estado y quién atiende. */
function actionsFor(
  status: ConversationStatus,
  mine: boolean,
): { primary: StatusAction; secondary: StatusAction[] } {
  switch (status) {
    case 'BotActive':
      return { primary: take, secondary: [resolve] }
    case 'WaitingHuman':
      return { primary: take, secondary: [toBot, resolve] }
    case 'HumanActive':
      return mine ? { primary: resolve, secondary: [toBot] } : { primary: take, secondary: [toBot, resolve] }
    case 'Resolved':
      return { primary: reopen, secondary: [reopenAndTake] }
  }
}

interface ChatHeaderProps {
  detail: InboxConversationDetail
  showBack: boolean
  contactOpen: boolean
  onToggleContact: () => void
}

export function ChatHeader({ detail, showBack, contactOpen, onToggleContact }: ChatHeaderProps) {
  const { user } = useSession()
  const search = inboxRouteApi.useSearch()
  const changeStatus = useChangeStatus(detail.id)
  const name = contactName(detail.contact, detail.channel)
  const phone = contactPhone(detail.contact, detail.channel)
  const mine = detail.assignedAgent?.id === user.id
  const { primary, secondary } = actionsFor(detail.status, mine)
  const pendingStatus = changeStatus.isPending ? changeStatus.variables : null

  return (
    <header className="bg-card @container flex h-16 shrink-0 items-center gap-2 border-b px-2 sm:px-3">
      {showBack && (
        <Button variant="ghost" size="icon" asChild>
          <Link to="/inbox" search={search} aria-label="Volver a las conversaciones">
            <ArrowLeft />
          </Link>
        </Button>
      )}
      <button
        type="button"
        onClick={onToggleContact}
        className="focus-visible:ring-ring/50 hover:bg-muted/60 flex min-w-0 items-center gap-3 rounded-lg px-1.5 py-1 text-left transition-colors outline-none focus-visible:ring-[3px]"
        aria-label={`Ver ficha de ${name}`}
      >
        <ContactAvatar name={name} avatarUrl={detail.contact.avatarUrl} channel={detail.channel} />
        <span className="min-w-0">
          <span className="block truncate text-sm font-semibold">{name}</span>
          <span className="text-muted-foreground flex min-w-0 items-center gap-1.5 text-xs">
            <span className="truncate">{phone ?? channelLabels[detail.channel]}</span>
            {detail.assignedAgent && detail.status === 'HumanActive' && (
              <span className="hidden truncate @xl:inline">
                · Atiende {mine ? 'tú' : agentName(detail.assignedAgent)}
              </span>
            )}
          </span>
        </span>
      </button>
      <StatusBadge status={detail.status} className="hidden @2xl:inline-flex" />

      <div className="ml-auto flex shrink-0 items-center gap-1">
        <LabelPicker detail={detail} />
        <Button
          size="sm"
          variant={detail.status === 'WaitingHuman' || detail.status === 'Resolved' ? 'default' : 'outline'}
          onClick={() => changeStatus.mutate(primary.status)}
          disabled={changeStatus.isPending}
          aria-label={primary.label}
          title={primary.label}
        >
          {pendingStatus === primary.status ? <Loader2 className="animate-spin" /> : <primary.icon />}
          <span className="hidden @lg:inline">{primary.label}</span>
        </Button>
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="icon-sm" aria-label="Más acciones">
              <EllipsisVertical />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-52">
            <DropdownMenuLabel className="text-muted-foreground text-xs font-normal">
              Conversación
            </DropdownMenuLabel>
            {secondary.map((action) => (
              <DropdownMenuItem
                key={action.label}
                disabled={changeStatus.isPending}
                onSelect={() => changeStatus.mutate(action.status)}
              >
                <action.icon /> {action.label}
              </DropdownMenuItem>
            ))}
            <DropdownMenuSeparator />
            <DropdownMenuItem onSelect={onToggleContact}>
              <PanelRight /> {contactOpen ? 'Ocultar ficha del contacto' : 'Ver ficha del contacto'}
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
        <Button
          variant="ghost"
          size="icon-sm"
          className={cn('hidden md:inline-flex', contactOpen && 'bg-muted')}
          aria-label="Ficha del contacto"
          aria-pressed={contactOpen}
          title={contactOpen ? 'Ocultar ficha del contacto' : 'Ver ficha del contacto'}
          onClick={onToggleContact}
        >
          <PanelRight />
        </Button>
      </div>
    </header>
  )
}

function LabelPicker({ detail }: { detail: InboxConversationDetail }) {
  const labels = useQuery(labelsQuery)
  const setLabel = useSetConversationLabel(detail.id)
  const [manageOpen, setManageOpen] = useState(false)
  const current = detail.label

  return (
    <>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button
            variant="ghost"
            size="sm"
            className="max-w-40"
            aria-label={current ? `Etiqueta: ${current.name}. Cambiar etiqueta` : 'Asignar etiqueta'}
            title={current ? current.name : 'Asignar etiqueta'}
          >
            {current ? <LabelDot color={current.color} className="size-2.5" /> : <Tag />}
            <span className="hidden truncate @3xl:inline">{current ? current.name : 'Etiqueta'}</span>
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="w-56">
          <DropdownMenuLabel>Etiqueta</DropdownMenuLabel>
          {labels.isPending ? (
            <div className="space-y-1.5 p-2">
              <Skeleton className="h-5 w-full" />
              <Skeleton className="h-5 w-2/3" />
            </div>
          ) : (
            <DropdownMenuRadioGroup
              value={current ? String(current.id) : 'none'}
              onValueChange={(v) => {
                if (v === 'none') return setLabel.mutate(null)
                const label = labels.data?.find((l) => String(l.id) === v)
                if (label) setLabel.mutate({ id: label.id, name: label.name, color: label.color })
              }}
            >
              <DropdownMenuRadioItem value="none">Sin etiqueta</DropdownMenuRadioItem>
              {labels.data?.map((label) => (
                <DropdownMenuRadioItem key={label.id} value={String(label.id)}>
                  <LabelDot color={label.color} />
                  <span className="truncate">{label.name}</span>
                </DropdownMenuRadioItem>
              ))}
            </DropdownMenuRadioGroup>
          )}
          <DropdownMenuSeparator />
          <DropdownMenuItem onSelect={() => setManageOpen(true)}>
            <Tags /> Gestionar etiquetas…
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
      <LabelManagerDialog open={manageOpen} onOpenChange={setManageOpen} />
    </>
  )
}

export function ChatHeaderSkeleton({ showBack }: { showBack: boolean }) {
  return (
    <div
      className="bg-card flex h-16 shrink-0 items-center gap-3 border-b px-3"
      role="status"
      aria-label="Cargando"
    >
      {showBack && <Skeleton className="size-9 rounded-md" />}
      <Skeleton className="size-10 rounded-full" />
      <div className="space-y-1.5">
        <Skeleton className="h-4 w-36" />
        <Skeleton className="h-3 w-24" />
      </div>
      <Skeleton className="ml-auto h-8 w-28" />
    </div>
  )
}
