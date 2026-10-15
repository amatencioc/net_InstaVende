import { format, parseISO } from 'date-fns'
import { es } from 'date-fns/locale'
import { useSession } from '@/lib/auth/session'
import type { ChannelType } from './api'

/**
 * Variación porcentual de `current` respecto a `previous`.
 * Devuelve null cuando no hay base de comparación (periodo anterior en cero).
 */
export function percentChange(current: number, previous: number): number | null {
  if (previous === 0) return current === 0 ? 0 : null
  return ((current - previous) / Math.abs(previous)) * 100
}

const compactFormatter = new Intl.NumberFormat('es-PE', { notation: 'compact', maximumFractionDigits: 1 })

export function formatCompact(value: number): string {
  return compactFormatter.format(value)
}

/** Importe compacto para ejes de gráficos ("S/ 12,5 mil"). */
export function useCompactMoney(): (amount: number) => string {
  const { business } = useSession()
  return (amount) => `${business.currencySymbol} ${formatCompact(amount)}`
}

/** Duración legible: "45 s", "3 min 20 s", "2 h 5 min". */
export function formatDuration(seconds: number | null | undefined): string {
  if (seconds == null) return '—'
  const s = Math.max(0, Math.round(seconds))
  if (s < 60) return `${s} s`
  if (s < 3600) {
    const m = Math.floor(s / 60)
    const rest = s % 60
    return rest ? `${m} min ${rest} s` : `${m} min`
  }
  const h = Math.floor(s / 3600)
  const m = Math.round((s % 3600) / 60)
  return m ? `${h} h ${m} min` : `${h} h`
}

/** "2026-10-14" → "14 oct". */
export function formatDayShort(isoDate: string): string {
  return format(parseISO(isoDate), 'd MMM', { locale: es })
}

/** "2026-10-14" → "martes, 14 oct 2026". */
export function formatDayLong(isoDate: string): string {
  return format(parseISO(isoDate), 'EEEE, d MMM yyyy', { locale: es })
}

/** Rango legible: "8 – 14 oct 2026", "28 sep – 4 oct 2026", "15 dic 2025 – 3 ene 2026". */
export function formatRange(from: string, to: string): string {
  const a = parseISO(from)
  const b = parseISO(to)
  if (from === to) return format(a, 'd MMM yyyy', { locale: es })
  if (a.getFullYear() !== b.getFullYear())
    return `${format(a, 'd MMM yyyy', { locale: es })} – ${format(b, 'd MMM yyyy', { locale: es })}`
  if (a.getMonth() !== b.getMonth())
    return `${format(a, 'd MMM', { locale: es })} – ${format(b, 'd MMM yyyy', { locale: es })}`
  return `${format(a, 'd', { locale: es })} – ${format(b, 'd MMM yyyy', { locale: es })}`
}

export function capitalize(text: string): string {
  return text.charAt(0).toUpperCase() + text.slice(1)
}

/** Clases de badge para las clases `st-*` que devuelve OrderWorkflow.BadgeCss. */
const subStatusTones: Record<string, { badge: string; bar: string }> = {
  'st-purple': { badge: 'bg-violet-500/12 text-violet-700 dark:text-violet-300', bar: 'bg-violet-500' },
  'st-blue': { badge: 'bg-blue-500/12 text-blue-700 dark:text-blue-300', bar: 'bg-blue-500' },
  'st-orange': { badge: 'bg-orange-500/12 text-orange-700 dark:text-orange-300', bar: 'bg-orange-500' },
  'st-sky': { badge: 'bg-sky-500/12 text-sky-700 dark:text-sky-300', bar: 'bg-sky-500' },
  'st-green': { badge: 'bg-emerald-500/12 text-emerald-700 dark:text-emerald-300', bar: 'bg-emerald-500' },
  'st-red': { badge: 'bg-red-500/12 text-red-700 dark:text-red-300', bar: 'bg-red-500' },
}
const neutralTone = { badge: 'bg-muted text-muted-foreground', bar: 'bg-muted-foreground' }

export function subStatusTone(css: string) {
  return subStatusTones[css] ?? neutralTone
}

/** Color de marca de cada canal (variables CSS de index.css). */
export const channelColors: Record<ChannelType, string> = {
  WhatsApp: 'var(--whatsapp)',
  Instagram: 'var(--instagram)',
  FacebookMessenger: 'var(--messenger)',
}

// ── CSV ──────────────────────────────────────────────────────────────────────

export type CsvCell = string | number | null | undefined

function csvCell(value: CsvCell): string {
  if (value == null) return ''
  const text = typeof value === 'number' ? String(Math.round(value * 100) / 100) : value
  return /[",\n\r]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text
}

/** Genera un CSV (UTF-8 con BOM para Excel) a partir de secciones con título y filas. */
export function buildCsv(sections: { title: string; rows: CsvCell[][] }[]): string {
  const lines: string[] = []
  sections.forEach((section, i) => {
    if (i > 0) lines.push('')
    lines.push(csvCell(section.title))
    for (const row of section.rows) lines.push(row.map(csvCell).join(','))
  })
  return '﻿' + lines.join('\r\n')
}

export function downloadTextFile(content: string, fileName: string, mime = 'text/csv;charset=utf-8') {
  const url = URL.createObjectURL(new Blob([content], { type: mime }))
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  link.remove()
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}
