import { z } from 'zod'
import type { DeliveryZone, DeliveryZoneUpsert, KnowledgeCategory, PaymentMethod, PaymentMethodType, PaymentMethodUpsert } from './api'
import { KNOWLEDGE_CONTENT_MAX, KNOWLEDGE_TITLE_MAX, knowledgeCategories, PAYMENT_INSTRUCTIONS_MAX, paymentTypes } from './meta'

const optionalText = (max: number, message: string) => z.string().trim().max(max, message)

const toNullable = (value: string) => (value.trim() === '' ? null : value.trim())

// ── Base de conocimiento ─────────────────────────────────────────────────────

export const knowledgeFormSchema = z.object({
  title: z
    .string()
    .trim()
    .min(1, 'El título es obligatorio.')
    .max(KNOWLEDGE_TITLE_MAX, `El título no puede superar los ${KNOWLEDGE_TITLE_MAX} caracteres.`),
  content: z
    .string()
    .trim()
    .min(1, 'El contenido es obligatorio.')
    .max(KNOWLEDGE_CONTENT_MAX, `El contenido no puede superar los ${KNOWLEDGE_CONTENT_MAX} caracteres.`),
  category: z.enum(knowledgeCategories as [KnowledgeCategory, ...KnowledgeCategory[]], {
    error: 'Selecciona una categoría.',
  }),
})
export type KnowledgeFormValues = z.infer<typeof knowledgeFormSchema>

// ── Zonas de entrega ─────────────────────────────────────────────────────────

/** Modo de costo: precio fijo, gratis (0) o a coordinar (null). */
export type CostMode = 'fixed' | 'free' | 'coordinate'

export const deliveryZoneFormSchema = z
  .object({
    name: z.string().trim().min(1, 'El nombre es obligatorio.').max(100, 'El nombre no puede superar los 100 caracteres.'),
    costMode: z.enum(['fixed', 'free', 'coordinate']),
    cost: z.string(),
    description: optionalText(500, 'La descripción no puede superar los 500 caracteres.'),
    isActive: z.boolean(),
  })
  .superRefine((values, ctx) => {
    if (values.costMode !== 'fixed') return
    const amount = Number(values.cost.replace(',', '.'))
    if (values.cost.trim() === '' || Number.isNaN(amount))
      ctx.addIssue({ code: 'custom', path: ['cost'], message: 'Ingresa el costo de envío.' })
    else if (amount < 0) ctx.addIssue({ code: 'custom', path: ['cost'], message: 'El costo debe ser 0 o mayor.' })
  })
export type DeliveryZoneFormValues = z.infer<typeof deliveryZoneFormSchema>

export function deliveryZoneDefaults(zone?: DeliveryZone): DeliveryZoneFormValues {
  const cost = zone?.cost ?? null
  return {
    name: zone?.name ?? '',
    costMode: !zone ? 'fixed' : cost === null ? 'coordinate' : cost === 0 ? 'free' : 'fixed',
    cost: cost !== null && cost > 0 ? String(cost) : '',
    description: zone?.description ?? '',
    isActive: zone?.isActive ?? true,
  }
}

export function toDeliveryZoneRequest(values: DeliveryZoneFormValues): DeliveryZoneUpsert {
  const cost =
    values.costMode === 'coordinate'
      ? null
      : values.costMode === 'free'
        ? 0
        : Math.round(Number(values.cost.replace(',', '.')) * 100) / 100
  return { name: values.name.trim(), cost, description: toNullable(values.description), isActive: values.isActive }
}

// ── Métodos de pago ──────────────────────────────────────────────────────────

const httpUrl = z
  .string()
  .trim()
  .max(500, 'El enlace no puede superar los 500 caracteres.')
  .refine((value) => {
    if (value === '') return true
    try {
      const url = new URL(value)
      return (url.protocol === 'http:' || url.protocol === 'https:') && url.hostname !== ''
    } catch {
      return false
    }
  }, 'Ingresa un enlace válido que empiece con http:// o https://.')

export const paymentMethodFormSchema = z.object({
  type: z.enum(paymentTypes as [PaymentMethodType, ...PaymentMethodType[]], { error: 'Selecciona un tipo.' }),
  name: z.string().trim().min(1, 'El nombre es obligatorio.').max(150, 'El nombre no puede superar los 150 caracteres.'),
  instructions: optionalText(
    PAYMENT_INSTRUCTIONS_MAX,
    `Las instrucciones no pueden superar los ${PAYMENT_INSTRUCTIONS_MAX} caracteres.`,
  ),
  accountNumber: optionalText(100, 'El número de cuenta no puede superar los 100 caracteres.'),
  accountAlias: optionalText(100, 'El alias no puede superar los 100 caracteres.'),
  paymentLink: httpUrl,
  isActive: z.boolean(),
})
export type PaymentMethodFormValues = z.infer<typeof paymentMethodFormSchema>

export function paymentMethodDefaults(method?: PaymentMethod): PaymentMethodFormValues {
  return {
    type: method?.type ?? 'BankTransfer',
    name: method?.name ?? '',
    instructions: method?.instructions ?? '',
    accountNumber: method?.accountNumber ?? '',
    accountAlias: method?.accountAlias ?? '',
    paymentLink: method?.paymentLink ?? '',
    isActive: method?.isActive ?? true,
  }
}

export function toPaymentMethodRequest(values: PaymentMethodFormValues): PaymentMethodUpsert {
  return {
    type: values.type,
    name: values.name.trim(),
    instructions: toNullable(values.instructions),
    accountNumber: toNullable(values.accountNumber),
    accountAlias: toNullable(values.accountAlias),
    paymentLink: toNullable(values.paymentLink),
    isActive: values.isActive,
  }
}
