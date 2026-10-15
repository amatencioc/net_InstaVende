import { Bot, CircleCheck, Hand, Headset, Inbox, BellDot, type LucideIcon } from 'lucide-react'
import { z } from 'zod'
import type { ChannelType, ConversationStatus, InboxCounts, InboxStatusFilter } from './api'
import type { Schemas } from '@/lib/api/client'

// ── Parámetros de búsqueda de /inbox ─────────────────────────────────────────

export const statusFilters = [
  'All',
  'Unattended',
  'BotActive',
  'WaitingHuman',
  'HumanActive',
  'Resolved',
] as const
export const channelTypes = ['WhatsApp', 'Instagram', 'FacebookMessenger'] as const

/** Filtros de la bandeja en la URL: la vista es enlazable y sobrevive a recargas. */
export const inboxSearchSchema = z.object({
  status: z.enum(statusFilters).optional().catch(undefined),
  channel: z.enum(channelTypes).optional().catch(undefined),
  label: z.number().int().positive().optional().catch(undefined),
  q: z.string().max(100).optional().catch(undefined),
})
export type InboxSearch = z.infer<typeof inboxSearchSchema>

// ── Categorías del panel lateral ─────────────────────────────────────────────

export const categories: ReadonlyArray<{
  value: InboxStatusFilter
  label: string
  icon: LucideIcon
  count: keyof InboxCounts
}> = [
  { value: 'All', label: 'Todas', icon: Inbox, count: 'all' },
  { value: 'Unattended', label: 'Sin atender', icon: BellDot, count: 'unattended' },
  { value: 'BotActive', label: 'Bot activo', icon: Bot, count: 'botActive' },
  { value: 'WaitingHuman', label: 'Esperando humano', icon: Hand, count: 'waitingHuman' },
  { value: 'HumanActive', label: 'En atención', icon: Headset, count: 'humanActive' },
  { value: 'Resolved', label: 'Resueltas', icon: CircleCheck, count: 'resolved' },
]

// ── Estados ──────────────────────────────────────────────────────────────────

export const statusMeta: Record<ConversationStatus, { label: string; icon: LucideIcon; className: string }> =
  {
    BotActive: { label: 'Bot activo', icon: Bot, className: 'bg-brand-accent/10 text-brand-accent' },
    WaitingHuman: { label: 'Esperando humano', icon: Hand, className: 'bg-warning/15 text-warning' },
    HumanActive: { label: 'En atención', icon: Headset, className: 'bg-info/10 text-info' },
    Resolved: { label: 'Resuelta', icon: CircleCheck, className: 'bg-muted text-muted-foreground' },
  }

export const channelLabels: Record<ChannelType, string> = {
  WhatsApp: 'WhatsApp',
  Instagram: 'Instagram',
  FacebookMessenger: 'Messenger',
}

type OrderSubStatus = Schemas['OrderSubStatus']

/** Color del badge de pedido según su sub-estado (equivalente a OrderWorkflow.BadgeCss). */
export function orderBadgeClass(subStatus: OrderSubStatus): string {
  switch (subStatus) {
    case 'EnValidacion':
      return 'bg-brand-accent/10 text-brand-accent'
    case 'EnPreparacion':
    case 'Enviado':
      return 'bg-info/10 text-info'
    case 'ListoParaEnvio':
      return 'bg-warning/15 text-warning'
    case 'Entregado':
    case 'Finalizado':
      return 'bg-success/10 text-success'
    default:
      return 'bg-destructive/10 text-destructive'
  }
}

/** Colores sugeridos para etiquetas (el usuario elige uno). */
export const labelColors = [
  '#6366f1',
  '#8b5cf6',
  '#ec4899',
  '#ef4444',
  '#f59e0b',
  '#22c55e',
  '#06b6d4',
  '#64748b',
] as const

// ── Contacto ─────────────────────────────────────────────────────────────────

interface ContactLike {
  name: string | null
  phone: string | null
  externalId: string
}

/** Teléfono legible: el registrado o, en WhatsApp, el id externo (número en formato internacional). */
export function contactPhone(contact: ContactLike, channel: ChannelType): string | null {
  if (contact.phone) return contact.phone
  if (channel === 'WhatsApp' && /^\d{6,}$/.test(contact.externalId)) return `+${contact.externalId}`
  return null
}

export function contactName(contact: ContactLike, channel: ChannelType): string {
  return contact.name?.trim() || contactPhone(contact, channel) || contact.externalId
}

export function agentName(agent: { firstName: string; lastName: string } | null | undefined): string {
  if (!agent) return ''
  return `${agent.firstName} ${agent.lastName}`.trim() || 'Agente'
}
