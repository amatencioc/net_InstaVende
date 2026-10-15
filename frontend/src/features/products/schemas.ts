import { z } from 'zod'
import type { ProductDetail, ProductListParams, ProductUpsertRequest } from './api'

// ── Estado de la URL del listado ─────────────────────────────────────────────

export const PAGE_SIZES = [10, 20, 50, 100] as const
export const DEFAULT_PAGE_SIZE = 20

export const productStatusValues = ['active', 'inactive', 'lowStock', 'outOfStock'] as const
export const productSortValues = [
  'newest',
  'oldest',
  'nameAsc',
  'nameDesc',
  'priceAsc',
  'priceDesc',
  'stockAsc',
  'stockDesc',
  'manual',
] as const

/**
 * Filtros, página y vista del listado en la URL (compartible y con historial).
 * `category`: id de la categoría o 0 para "sin categoría". Valores inválidos se descartan.
 */
export const productSearchSchema = z.object({
  q: z.string().trim().max(100).optional().catch(undefined),
  category: z.number().int().min(0).optional().catch(undefined),
  status: z.enum(productStatusValues).optional().catch(undefined),
  sort: z.enum(productSortValues).optional().catch(undefined),
  page: z.number().int().min(1).optional().catch(undefined),
  pageSize: z
    .number()
    .int()
    .refine((n) => (PAGE_SIZES as readonly number[]).includes(n))
    .optional()
    .catch(undefined),
  view: z.enum(['table', 'grid']).optional().catch(undefined),
})
export type ProductSearch = z.infer<typeof productSearchSchema>

export function toListParams(search: ProductSearch): ProductListParams {
  return {
    search: search.q || undefined,
    categoryId: search.category,
    status: search.status,
    sort: search.sort,
    page: search.page ?? 1,
    pageSize: search.pageSize ?? DEFAULT_PAGE_SIZE,
  }
}

export function hasActiveFilters(search: ProductSearch): boolean {
  return !!search.q || search.category !== undefined || search.status !== undefined
}

// ── Formulario de producto ───────────────────────────────────────────────────

export const NAME_MAX = 150
export const DESCRIPTION_MAX = 2000
export const SKU_MAX = 64
export const MAX_IMAGES = 6
export const MAX_VARIANTS = 100
const MAX_PRICE = 99_999_999.99

const money = (label: string) =>
  z
    .number({ error: `Ingresa ${label} válido.` })
    .min(0, `${label.charAt(0).toUpperCase()}${label.slice(1)} no puede ser negativo.`)
    .max(MAX_PRICE, 'El importe es demasiado alto.')

export const variantFormSchema = z.object({
  id: z.number().int().nullable(),
  attributeName: z.string().trim().min(1, 'Indica la opción.').max(50, 'Máximo 50 caracteres.'),
  attributeValue: z.string().trim().min(1, 'Indica el valor.').max(100, 'Máximo 100 caracteres.'),
  priceModifier: z
    .number({ error: 'Ingresa un número.' })
    .min(-MAX_PRICE, 'Importe no válido.')
    .max(MAX_PRICE, 'Importe no válido.'),
  stock: z.number({ error: 'Ingresa un número.' }).int('Usa un número entero.').min(0, 'No puede ser negativo.'),
  sku: z.string().trim().max(SKU_MAX, `Máximo ${SKU_MAX} caracteres.`),
  isActive: z.boolean(),
})
export type VariantFormValues = z.infer<typeof variantFormSchema>

export const productFormSchema = z
  .object({
    name: z
      .string()
      .trim()
      .min(1, 'El nombre es obligatorio.')
      .max(NAME_MAX, `El nombre admite como máximo ${NAME_MAX} caracteres.`),
    description: z.string().max(DESCRIPTION_MAX, `La descripción admite como máximo ${DESCRIPTION_MAX} caracteres.`),
    sku: z.string().trim().max(SKU_MAX, `El SKU admite como máximo ${SKU_MAX} caracteres.`),
    categoryId: z.number().int().nullable(),
    isActive: z.boolean(),
    isFeatured: z.boolean(),
    price: money('un precio'),
    priceOriginal: money('un precio anterior').nullable(),
    stock: z.number({ error: 'Ingresa un número.' }).int('Usa un número entero.').min(0, 'El stock no puede ser negativo.'),
    variants: z.array(variantFormSchema).max(MAX_VARIANTS, `Máximo ${MAX_VARIANTS} variantes.`),
  })
  .superRefine((values, ctx) => {
    if (values.priceOriginal != null && values.priceOriginal < values.price) {
      ctx.addIssue({
        code: 'custom',
        path: ['priceOriginal'],
        message: 'El precio anterior debe ser mayor o igual al precio actual.',
      })
    }
    const seen = new Map<string, number>()
    values.variants.forEach((v, index) => {
      const key = `${v.attributeName.trim().toLowerCase()}::${v.attributeValue.trim().toLowerCase()}`
      if (!v.attributeName.trim() || !v.attributeValue.trim()) return
      if (seen.has(key)) {
        ctx.addIssue({ code: 'custom', path: ['variants', index, 'attributeValue'], message: 'Esta variante está repetida.' })
      } else seen.set(key, index)
    })
    const skus = new Set<string>()
    values.variants.forEach((v, index) => {
      const sku = v.sku.trim().toLowerCase()
      if (!sku) return
      if (skus.has(sku)) ctx.addIssue({ code: 'custom', path: ['variants', index, 'sku'], message: 'SKU repetido.' })
      skus.add(sku)
    })
  })
export type ProductFormValues = z.infer<typeof productFormSchema>

export const emptyProductForm: ProductFormValues = {
  name: '',
  description: '',
  sku: '',
  categoryId: null,
  isActive: true,
  isFeatured: false,
  price: 0,
  priceOriginal: null,
  stock: 0,
  variants: [],
}

export function newVariant(attributeName = '', attributeValue = ''): VariantFormValues {
  return { id: null, attributeName, attributeValue, priceModifier: 0, stock: 0, sku: '', isActive: true }
}

export function detailToForm(p: ProductDetail): ProductFormValues {
  return {
    name: p.name,
    description: p.description ?? '',
    sku: p.sku ?? '',
    categoryId: p.categoryId ?? null,
    isActive: p.isActive,
    isFeatured: p.isFeatured,
    price: p.price,
    priceOriginal: p.priceOriginal ?? null,
    stock: p.stock,
    variants: p.variants.map((v) => ({
      id: v.id,
      attributeName: v.attributeName,
      attributeValue: v.attributeValue,
      priceModifier: v.priceModifier,
      stock: v.stock,
      sku: v.sku ?? '',
      isActive: v.isActive,
    })),
  }
}

/** Convierte el formulario en la petición de la API (sin sortOrder: el servidor conserva el actual). */
export function formToRequest(v: ProductFormValues): ProductUpsertRequest {
  return {
    name: v.name.trim(),
    description: v.description.trim() || null,
    sku: v.sku.trim() || null,
    price: v.price,
    priceOriginal: v.priceOriginal,
    stock: v.stock,
    categoryId: v.categoryId,
    isActive: v.isActive,
    isFeatured: v.isFeatured,
    variants: v.variants.map((x) => ({
      id: x.id,
      attributeName: x.attributeName.trim(),
      attributeValue: x.attributeValue.trim(),
      priceModifier: x.priceModifier,
      stock: x.stock,
      sku: x.sku.trim() || null,
      isActive: x.isActive,
    })),
  }
}

// ── Categorías ───────────────────────────────────────────────────────────────

export const categoryFormSchema = z.object({
  name: z.string().trim().min(1, 'El nombre es obligatorio.').max(100, 'Máximo 100 caracteres.'),
})
