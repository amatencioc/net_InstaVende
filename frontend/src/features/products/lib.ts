import { apiBaseUrl } from '@/lib/api/client'
import type { ProductSort, ProductStatusFilter } from './api'

/** Umbral de "stock bajo" (igual que en la API: 1..5 unidades). */
export const LOW_STOCK_THRESHOLD = 5

/** URL absoluta de una imagen subida (la API devuelve rutas relativas /uploads/...). */
export function imageSrc(url: string | null | undefined): string | undefined {
  if (!url) return undefined
  return url.startsWith('/') ? `${apiBaseUrl}${url}` : url
}

export type Availability = 'inactive' | 'outOfStock' | 'lowStock' | 'inStock'

export function availabilityOf(p: { isActive: boolean; stock: number }): Availability {
  if (!p.isActive) return 'inactive'
  if (p.stock <= 0) return 'outOfStock'
  if (p.stock <= LOW_STOCK_THRESHOLD) return 'lowStock'
  return 'inStock'
}

export const statusLabels: Record<ProductStatusFilter, string> = {
  active: 'Disponibles',
  inactive: 'No disponibles',
  lowStock: 'Stock bajo',
  outOfStock: 'Agotados',
}

export const sortLabels: Record<ProductSort, string> = {
  newest: 'Más recientes',
  oldest: 'Más antiguos',
  nameAsc: 'Nombre (A-Z)',
  nameDesc: 'Nombre (Z-A)',
  priceAsc: 'Precio: menor a mayor',
  priceDesc: 'Precio: mayor a menor',
  stockAsc: 'Stock: menor a mayor',
  stockDesc: 'Stock: mayor a menor',
  manual: 'Orden del catálogo',
}

/** "1 producto" / "3 productos". */
export function plural(count: number, singular: string, pluralForm: string): string {
  return `${count} ${count === 1 ? singular : pluralForm}`
}

// ── Imágenes pendientes (formulario de alta: se suben tras crear el producto) ──

export const MAX_IMAGE_BYTES = 5 * 1024 * 1024
export const ACCEPTED_IMAGE_TYPES = {
  'image/jpeg': ['.jpg', '.jpeg'],
  'image/png': ['.png'],
  'image/webp': ['.webp'],
  'image/gif': ['.gif'],
}

export interface PendingImage {
  id: string
  file: File
  preview: string
}

export function toPendingImages(files: File[]): PendingImage[] {
  return files.map((file) => ({
    id: `local-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`,
    file,
    preview: URL.createObjectURL(file),
  }))
}

export function revokePendingImages(images: PendingImage[]) {
  images.forEach((img) => URL.revokeObjectURL(img.preview))
}
