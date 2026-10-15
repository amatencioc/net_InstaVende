import { z } from 'zod'
import { MAX_SCHEDULE_DAYS, REMINDER_MESSAGE_MAX, TEMPLATE_MESSAGE_MAX } from './constants'

const contactSchema = z.object({
  id: z.number(),
  name: z.string().nullable(),
  phone: z.string().nullable(),
  avatarUrl: z.string().nullable(),
  channel: z.enum(['WhatsApp', 'FacebookMessenger', 'Instagram']),
})
export type ReminderContactValue = z.infer<typeof contactSchema>

/** Formulario de recordatorio programado. `scheduledAt` es el valor de un input datetime-local. */
export const reminderFormSchema = z.object({
  contact: contactSchema.nullable().refine((c) => c !== null, 'Selecciona un contacto.'),
  scheduledAt: z
    .string()
    .min(1, 'Indica la fecha y hora de envío.')
    .refine((v) => !Number.isNaN(new Date(v).getTime()), 'La fecha no es válida.')
    .refine(
      (v) => new Date(v).getTime() > Date.now(),
      'La fecha de envío debe ser posterior a la hora actual.',
    )
    .refine(
      (v) => new Date(v).getTime() <= Date.now() + MAX_SCHEDULE_DAYS * 86_400_000,
      `Solo puedes programar hasta ${MAX_SCHEDULE_DAYS} días en el futuro.`,
    ),
  message: z
    .string()
    .trim()
    .min(1, 'Escribe el mensaje del recordatorio.')
    .max(REMINDER_MESSAGE_MAX, `El mensaje no puede superar los ${REMINDER_MESSAGE_MAX} caracteres.`),
})
/** Valores del formulario (el contacto puede estar vacío mientras se edita). */
export type ReminderFormInput = z.input<typeof reminderFormSchema>
/** Valores validados (contacto garantizado). */
export type ReminderFormValues = z.output<typeof reminderFormSchema>

/** Valida el mensaje de una plantilla de segmento; devuelve el error o null. */
export function templateMessageError(message: string): string | null {
  const text = message.trim()
  if (!text) return 'Escribe el mensaje del recordatorio.'
  if (text.length > TEMPLATE_MESSAGE_MAX) return `Máximo ${TEMPLATE_MESSAGE_MAX} caracteres.`
  return null
}
