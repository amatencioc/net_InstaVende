import { z } from 'zod'

/** Mismas reglas que el servidor (SettingsPatterns.Phone). */
const phonePattern = /^\+?[0-9\s().-]{6,25}$/

/** Campo de texto opcional: vacío se envía como null. */
const optionalText = (max: number, message: string) => z.string().trim().max(max, message)

const optionalPhone = z
  .string()
  .trim()
  .refine((v) => v === '' || phonePattern.test(v), 'Ingresa un teléfono válido (solo números, espacios, paréntesis o guiones).')

/** Contraseña con las reglas de Identity: mínimo 8 caracteres y al menos un número. */
export const passwordSchema = z
  .string()
  .min(8, 'La contraseña debe tener al menos 8 caracteres.')
  .max(100, 'La contraseña admite hasta 100 caracteres.')
  .regex(/\d/, 'La contraseña debe incluir al menos un número.')

export const profileSchema = z.object({
  firstName: z.string().trim().min(1, 'El nombre es obligatorio.').max(100, 'Máximo 100 caracteres.'),
  lastName: z.string().trim().min(1, 'El apellido es obligatorio.').max(100, 'Máximo 100 caracteres.'),
  phoneNumber: optionalPhone,
})
export type ProfileFormValues = z.infer<typeof profileSchema>

export const changePasswordSchema = z
  .object({
    currentPassword: z.string().min(1, 'Ingresa tu contraseña actual.'),
    newPassword: passwordSchema,
    confirmPassword: z.string(),
  })
  .refine((v) => v.newPassword === v.confirmPassword, {
    path: ['confirmPassword'],
    message: 'Las contraseñas no coinciden.',
  })
  .refine((v) => v.newPassword !== v.currentPassword || v.newPassword === '', {
    path: ['newPassword'],
    message: 'La nueva contraseña debe ser distinta de la actual.',
  })
export type ChangePasswordFormValues = z.infer<typeof changePasswordSchema>

export const changeEmailSchema = z.object({
  newEmail: z.email('Ingresa un email válido.').max(256),
  currentPassword: z.string().min(1, 'Confirma con tu contraseña actual.'),
})
export type ChangeEmailFormValues = z.infer<typeof changeEmailSchema>

export const businessSchema = z.object({
  name: z.string().trim().min(1, 'El nombre del negocio es obligatorio.').max(200, 'Máximo 200 caracteres.'),
  description: optionalText(1000, 'Máximo 1000 caracteres.'),
  sector: optionalText(100, 'Máximo 100 caracteres.'),
  country: z.string().min(1, 'Selecciona el país.'),
  currency: z.string().min(1, 'Selecciona la moneda.'),
  phone: optionalPhone,
  whatsAppNumber: optionalPhone,
  email: z
    .string()
    .trim()
    .max(256)
    .refine((v) => v === '' || z.email().safeParse(v).success, 'Ingresa un email válido.'),
  websiteUrl: z
    .string()
    .trim()
    .max(500, 'Máximo 500 caracteres.')
    .refine((v) => v === '' || isHttpUrl(v), 'Ingresa una URL válida que empiece por http:// o https://.'),
  socialMedia: optionalText(500, 'Máximo 500 caracteres.'),
})
export type BusinessFormValues = z.infer<typeof businessSchema>

export const inviteSchema = z.object({
  email: z.email('Ingresa un email válido.').max(256),
  role: z.enum(['Admin', 'Member']),
})
export type InviteFormValues = z.infer<typeof inviteSchema>

export const notificationEmailSchema = z.object({
  email: z.email('Ingresa un email válido.').max(256),
})

export const acceptInvitationSchema = z
  .object({
    firstName: z.string().trim().min(1, 'El nombre es obligatorio.').max(100),
    lastName: z.string().trim().min(1, 'El apellido es obligatorio.').max(100),
    password: passwordSchema,
    confirmPassword: z.string(),
  })
  .refine((v) => v.password === v.confirmPassword, {
    path: ['confirmPassword'],
    message: 'Las contraseñas no coinciden.',
  })
export type AcceptInvitationFormValues = z.infer<typeof acceptInvitationSchema>

export function isHttpUrl(value: string): boolean {
  try {
    const url = new URL(value)
    return (url.protocol === 'http:' || url.protocol === 'https:') && url.hostname !== ''
  } catch {
    return false
  }
}

/** Convierte "" en null para los campos opcionales que se envían a la API. */
export function nullIfEmpty(value: string): string | null {
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}
