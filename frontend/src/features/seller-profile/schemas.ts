import { z } from 'zod'
import type { QuickReply, QuickReplyUpsert, SellerProfile, SellerProfileUpdate, SellerRules, SellerRulesUpdate } from './api'

const max = (n: number) => `Máximo ${n} caracteres.`
const text = (n: number) => z.string().max(n, max(n))

// ── Personalidad ────────────────────────────────────────────────────────────

export const genderOptions = [
  { value: 'Masculino', label: 'Masculino' },
  { value: 'Femenino', label: 'Femenino' },
  { value: '', label: 'No especificado' },
] as const

export const responseLengthLabels = ['Muy conciso', 'Conciso', 'Equilibrado', 'Detallado', 'Muy detallado'] as const

export const responseLengthHints = [
  '1 oración por respuesta.',
  '1 a 2 oraciones por respuesta.',
  '2 a 3 oraciones por respuesta.',
  '3 a 4 oraciones por respuesta.',
  'Hasta 5 oraciones por respuesta.',
] as const

export const profileSchema = z.object({
  vendorName: z.string().trim().min(1, 'El nombre del vendedor es obligatorio.').max(60, max(60)),
  vendorGender: z.enum(['', 'Masculino', 'Femenino']),
  country: z.string().min(1, 'Selecciona el país de operación.'),
  businessDescription: text(1000),
  targetAudience: text(300),
  rules: text(2000),
  communicationStyle: text(200),
  salesStyle: text(210),
  responseLength: z.number().int().min(1).max(5),
  useEmojis: z.boolean(),
  emojiPalette: z.array(z.string()).max(24, 'Máximo 24 emojis.'),
  useOpeningPunctuation: z.boolean(),
  wordsToAvoid: z.array(z.string().max(60, max(60))).max(50, 'Máximo 50 palabras.'),
  welcomeMessage: z.string().trim().min(1, 'El mensaje de bienvenida es obligatorio.').max(500, max(500)),
  purchaseConfirmationMessage: text(500),
  humanHandoffSituations: z.array(z.string().max(200, max(200))).max(20, 'Máximo 20 situaciones.'),
  autoPauseOnHandoff: z.boolean(),
  handoffMessage: text(300),
})
export type ProfileFormValues = z.infer<typeof profileSchema>

export function toProfileForm(p: SellerProfile): ProfileFormValues {
  return {
    vendorName: p.vendorName,
    vendorGender: p.vendorGender === 'Masculino' || p.vendorGender === 'Femenino' ? p.vendorGender : '',
    country: p.country,
    businessDescription: p.businessDescription ?? '',
    targetAudience: p.targetAudience ?? '',
    rules: p.rules ?? '',
    communicationStyle: p.communicationStyle ?? '',
    salesStyle: p.salesStyle ?? '',
    responseLength: p.responseLength,
    useEmojis: p.useEmojis,
    emojiPalette: [...p.emojiPalette],
    useOpeningPunctuation: p.useOpeningPunctuation,
    wordsToAvoid: [...p.wordsToAvoid],
    welcomeMessage: p.welcomeMessage,
    purchaseConfirmationMessage: p.purchaseConfirmationMessage ?? '',
    humanHandoffSituations: [...p.humanHandoffSituations],
    autoPauseOnHandoff: p.autoPauseOnHandoff,
    handoffMessage: p.handoffMessage ?? '',
  }
}

const orNull = (v: string) => (v.trim() === '' ? null : v.trim())

export function toProfileRequest(v: ProfileFormValues): SellerProfileUpdate {
  return {
    ...v,
    vendorGender: v.vendorGender === '' ? null : v.vendorGender,
    businessDescription: orNull(v.businessDescription),
    targetAudience: orNull(v.targetAudience),
    rules: orNull(v.rules),
    communicationStyle: orNull(v.communicationStyle),
    salesStyle: orNull(v.salesStyle),
    purchaseConfirmationMessage: orNull(v.purchaseConfirmationMessage),
    handoffMessage: orNull(v.handoffMessage),
  }
}

// ── Reglas y políticas ──────────────────────────────────────────────────────

const percent = z
  .number({ error: 'Ingresa un número.' })
  .min(0, 'Mínimo 0 %.')
  .max(100, 'Máximo 100 %.')

export const rulesSchema = z
  .object({
    isActive: z.boolean(),
    botName: z.string().trim().min(1, 'El nombre interno es obligatorio.').max(60, max(60)),
    fallbackMessage: z.string().trim().min(1, 'El mensaje de respaldo es obligatorio.').max(500, max(500)),
    enableHandoff: z.boolean(),
    handoffTriggerPhrase: text(100),
    businessHours: text(200),
    returnPolicy: text(2000),
    warrantyPolicy: text(2000),
    shippingTimes: text(1000),
    activePromotions: text(2000),
    discountLevel1: percent,
    discountLevel2: percent,
    discountLevel3: percent,
    maxDiscountPercent: percent,
    loyaltyDiscount: percent,
    bundleDiscount: percent,
    freeShippingThreshold: z.number({ error: 'Ingresa un monto.' }).min(0, 'No puede ser negativo.'),
  })
  .superRefine((v, ctx) => {
    if (v.discountLevel2 < v.discountLevel1)
      ctx.addIssue({ code: 'custom', path: ['discountLevel2'], message: 'Debe ser mayor o igual al nivel 1.' })
    if (v.discountLevel3 < v.discountLevel2)
      ctx.addIssue({ code: 'custom', path: ['discountLevel3'], message: 'Debe ser mayor o igual al nivel 2.' })
    if (v.discountLevel3 > v.maxDiscountPercent)
      ctx.addIssue({ code: 'custom', path: ['maxDiscountPercent'], message: 'Debe ser mayor o igual al nivel 3.' })
    const phrase = v.handoffTriggerPhrase.trim()
    if (v.enableHandoff && phrase.length > 0 && phrase.length < 3)
      ctx.addIssue({ code: 'custom', path: ['handoffTriggerPhrase'], message: 'Mínimo 3 caracteres.' })
  })
export type RulesFormValues = z.infer<typeof rulesSchema>

export function toRulesForm(r: SellerRules): RulesFormValues {
  return {
    isActive: r.isActive,
    botName: r.botName,
    fallbackMessage: r.fallbackMessage,
    enableHandoff: r.enableHandoff,
    handoffTriggerPhrase: r.handoffTriggerPhrase ?? '',
    businessHours: r.businessHours ?? '',
    returnPolicy: r.returnPolicy ?? '',
    warrantyPolicy: r.warrantyPolicy ?? '',
    shippingTimes: r.shippingTimes ?? '',
    activePromotions: r.activePromotions ?? '',
    discountLevel1: r.discountLevel1,
    discountLevel2: r.discountLevel2,
    discountLevel3: r.discountLevel3,
    maxDiscountPercent: r.maxDiscountPercent,
    loyaltyDiscount: r.loyaltyDiscount,
    bundleDiscount: r.bundleDiscount,
    freeShippingThreshold: r.freeShippingThreshold,
  }
}

export function toRulesRequest(v: RulesFormValues): SellerRulesUpdate {
  return {
    ...v,
    handoffTriggerPhrase: orNull(v.handoffTriggerPhrase),
    businessHours: orNull(v.businessHours),
    returnPolicy: orNull(v.returnPolicy),
    warrantyPolicy: orNull(v.warrantyPolicy),
    shippingTimes: orNull(v.shippingTimes),
    activePromotions: orNull(v.activePromotions),
  }
}

// ── Respuestas fijas ────────────────────────────────────────────────────────

export const quickReplySchema = z.object({
  name: z.string().trim().min(1, 'El nombre es obligatorio.').max(200, max(200)),
  triggerPhrases: z
    .array(z.string().min(3, 'Cada frase debe tener al menos 3 caracteres.').max(100, max(100)))
    .min(1, 'Agrega al menos una frase disparadora.')
    .max(20, 'Máximo 20 frases.'),
  response: z.string().trim().min(1, 'La respuesta es obligatoria.').max(1000, max(1000)),
  priority: z.number({ error: 'Ingresa un número.' }).int('Usa un número entero.').min(0, 'Mínimo 0.').max(100, 'Máximo 100.'),
  isActive: z.boolean(),
})
export type QuickReplyFormValues = z.infer<typeof quickReplySchema>

export function toQuickReplyForm(r?: QuickReply): QuickReplyFormValues {
  return r
    ? { name: r.name, triggerPhrases: [...r.triggerPhrases], response: r.response, priority: r.priority, isActive: r.isActive }
    : { name: '', triggerPhrases: [], response: '', priority: 0, isActive: true }
}

export const toQuickReplyRequest = (v: QuickReplyFormValues): QuickReplyUpsert => ({ ...v })
