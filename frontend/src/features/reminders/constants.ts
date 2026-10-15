import { CheckCircle2, Clock, Flame, Snowflake, Sun, XCircle, Ban, type LucideIcon } from 'lucide-react'
import type { CustomerSegment, ReminderCounts, ReminderStatus } from './api'

/** Límite del mensaje de un recordatorio programado (coincide con la API). */
export const REMINDER_MESSAGE_MAX = 1000
/** Límite del mensaje de las plantillas por segmento (coincide con la API). */
export const TEMPLATE_MESSAGE_MAX = 200
/** Antelación máxima permitida al programar (coincide con la API). */
export const MAX_SCHEDULE_DAYS = 90

export const statusMeta: Record<
  ReminderStatus,
  { label: string; tabLabel: string; icon: LucideIcon; className: string; countKey: keyof ReminderCounts }
> = {
  Pending: {
    label: 'Pendiente',
    tabLabel: 'Pendientes',
    icon: Clock,
    className: 'bg-info/10 text-info border-info/20',
    countKey: 'pending',
  },
  Sent: {
    label: 'Enviado',
    tabLabel: 'Enviados',
    icon: CheckCircle2,
    className: 'bg-success/10 text-success border-success/20',
    countKey: 'sent',
  },
  Failed: {
    label: 'Fallido',
    tabLabel: 'Fallidos',
    icon: XCircle,
    className: 'bg-destructive/10 text-destructive border-destructive/20',
    countKey: 'failed',
  },
  Cancelled: {
    label: 'Cancelado',
    tabLabel: 'Cancelados',
    icon: Ban,
    className: 'bg-muted text-muted-foreground border-border',
    countKey: 'cancelled',
  },
}

export const reminderStatuses = Object.keys(statusMeta) as ReminderStatus[]

export interface SegmentMeta {
  label: string
  icon: LucideIcon
  /** Clases del icono/acento del segmento (tokens del tema). */
  accent: string
  summary: string
  description: string
  tip: string
}

export const segmentMeta: Record<CustomerSegment, SegmentMeta> = {
  Frio: {
    label: 'Frío',
    icon: Snowflake,
    accent: 'bg-info/10 text-info',
    summary: 'Escribió pero aún no mostró interés concreto.',
    description:
      'Contactos que saludaron o preguntaron algo general y luego dejaron de responder. Un único recordatorio amable a las 2-3 horas reabre la conversación sin incomodar.',
    tip: 'Para contactos fríos es mejor no insistir demasiado: un solo mensaje, breve y útil.',
  },
  Tibio: {
    label: 'Tibio',
    icon: Sun,
    accent: 'bg-warning/15 text-warning',
    summary: 'Preguntó por productos, precios o envíos.',
    description:
      'Contactos que ya consultaron por un producto pero no avanzaron con la compra. Un primer recordatorio resuelve dudas y un segundo, al día siguiente, retoma el interés.',
    tip: 'El segundo recordatorio es opcional: desactívalo si prefieres un único mensaje estratégico.',
  },
  Caliente: {
    label: 'Caliente',
    icon: Flame,
    accent: 'bg-destructive/10 text-destructive',
    summary: 'Tiene un pedido en curso o está por pagar.',
    description:
      'Contactos con carrito o pedido a medio confirmar. Recuérdales lo que les falta (pago, dirección) y que su pedido sigue apartado para cerrar la venta.',
    tip: 'Sé concreto: menciona el siguiente paso para completar el pedido.',
  },
}

export const customerSegments = Object.keys(segmentMeta) as CustomerSegment[]

export const reminderOrderTitle: Record<number, string> = {
  1: 'Primer recordatorio',
  2: 'Segundo recordatorio',
}
