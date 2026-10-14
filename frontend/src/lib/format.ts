import { format, formatDistanceToNowStrict, isThisYear, isToday, isYesterday } from 'date-fns'
import { es } from 'date-fns/locale'

const numberFormatter = new Intl.NumberFormat('es-PE', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const integerFormatter = new Intl.NumberFormat('es-PE')

/** Importe con el símbolo de moneda del negocio (p.ej. "S/ 1,250.00"). Ver useMoney(). */
export function formatMoney(amount: number | null | undefined, symbol = 'S/'): string {
  return `${symbol} ${numberFormatter.format(amount ?? 0)}`
}

export function formatNumber(value: number | null | undefined): string {
  return integerFormatter.format(value ?? 0)
}

export function formatPercent(value: number | null | undefined, digits = 1): string {
  return `${(value ?? 0).toFixed(digits)}%`
}

/** Fecha y hora en la zona del navegador a partir de ISO-8601 UTC. */
export function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return ''
  return format(new Date(iso), 'd MMM yyyy, HH:mm', { locale: es })
}

export function formatDate(iso: string | null | undefined): string {
  if (!iso) return ''
  return format(new Date(iso), 'd MMM yyyy', { locale: es })
}

/** Etiqueta compacta para listas de conversaciones: "14:32", "Ayer", "lunes", "12 mar", "12/03/2025". */
export function formatChatTimestamp(iso: string | null | undefined): string {
  if (!iso) return ''
  const d = new Date(iso)
  if (isToday(d)) return format(d, 'HH:mm')
  if (isYesterday(d)) return 'Ayer'
  const days = (Date.now() - d.getTime()) / 86_400_000
  if (days < 7) return format(d, 'EEEE', { locale: es })
  return format(d, isThisYear(d) ? 'd MMM' : 'dd/MM/yyyy', { locale: es })
}

export function formatRelative(iso: string | null | undefined): string {
  if (!iso) return ''
  return formatDistanceToNowStrict(new Date(iso), { addSuffix: true, locale: es })
}

/** Valor para <input type="datetime-local"> en hora local del navegador. */
export function toDateTimeLocalInput(iso: string | null | undefined): string {
  const d = iso ? new Date(iso) : new Date()
  return format(d, "yyyy-MM-dd'T'HH:mm")
}

/** Convierte el valor de un <input type="datetime-local"> a ISO-8601 UTC. */
export function fromDateTimeLocalInput(value: string): string {
  return new Date(value).toISOString()
}

export function initials(name: string | null | undefined): string {
  const parts = (name ?? '').trim().split(/\s+/).filter(Boolean)
  if (parts.length === 0) return '?'
  const first = parts[0]?.[0] ?? ''
  const last = parts.length > 1 ? (parts.at(-1)?.[0] ?? '') : ''
  return (first + last).toUpperCase()
}
