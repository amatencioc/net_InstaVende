import { queryOptions, useMutation, useQueryClient } from '@tanstack/react-query'
import { api, ensureCsrf, unwrap, type Schemas } from '@/lib/api/client'
import { sessionKey, type Session } from '@/lib/auth/session'

export type SettingsProfile = Schemas['SettingsProfileDto']
export type SettingsBusiness = Schemas['SettingsBusinessDto']
export type NotificationEmail = Schemas['SettingsNotificationEmailDto']
export type NotificationEmails = Schemas['SettingsNotificationEmailsDto']
export type Team = Schemas['TeamDto']
export type TeamMember = Schemas['TeamMemberDto']
export type TeamInvitation = Schemas['TeamInvitationDto']
export type TeamInvitationCreated = Schemas['TeamInvitationCreatedDto']
export type UserRole = Schemas['UserRole']
export type InvitationDetails = Schemas['InvitationDetailsDto']

export const roleLabels: Record<UserRole, string> = { Admin: 'Administrador', Member: 'Miembro' }

export const settingsKeys = {
  all: ['settings'] as const,
  profile: ['settings', 'profile'] as const,
  business: ['settings', 'business'] as const,
  notificationEmails: ['settings', 'notification-emails'] as const,
}
export const teamKey = ['team'] as const
export const invitationKey = (token: string) => ['invitation', token] as const

/** Cuerpo multipart con un único archivo "file" para openapi-fetch (el navegador pone el boundary). */
function fileBody(file: File) {
  return {
    body: {},
    bodySerializer: () => {
      const form = new FormData()
      form.append('file', file)
      return form
    },
  }
}

// ── Perfil ───────────────────────────────────────────────────────────────────

export const profileQuery = queryOptions({
  queryKey: settingsKeys.profile,
  queryFn: () => unwrap(api.GET('/api/settings/profile')),
})

export function useUpdateProfile() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: Schemas['SettingsProfileUpdateRequest']) => unwrap(api.PUT('/api/settings/profile', { body })),
    meta: { silent: true, successMessage: 'Perfil actualizado.' },
    onSuccess: (profile) => {
      qc.setQueryData(settingsKeys.profile, profile)
      void qc.invalidateQueries({ queryKey: sessionKey })
      void qc.invalidateQueries({ queryKey: teamKey })
    },
  })
}

export function useUploadAvatar() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (file: File) => unwrap(api.POST('/api/settings/profile/avatar', fileBody(file))),
    meta: { successMessage: 'Foto de perfil actualizada.' },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: settingsKeys.profile })
      void qc.invalidateQueries({ queryKey: sessionKey })
      void qc.invalidateQueries({ queryKey: teamKey })
    },
  })
}

export function useDeleteAvatar() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: () => unwrap(api.DELETE('/api/settings/profile/avatar')),
    meta: { successMessage: 'Foto de perfil eliminada.' },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: settingsKeys.profile })
      void qc.invalidateQueries({ queryKey: sessionKey })
      void qc.invalidateQueries({ queryKey: teamKey })
    },
  })
}

// ── Seguridad ────────────────────────────────────────────────────────────────

export function useChangePassword() {
  return useMutation({
    mutationFn: (body: Schemas['SettingsChangePasswordRequest']) => unwrap(api.POST('/api/settings/password', { body })),
    meta: { silent: true, successMessage: 'Contraseña actualizada. Cerramos tus sesiones en otros dispositivos.' },
  })
}

export function useChangeEmail() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: Schemas['SettingsChangeEmailRequest']) => unwrap(api.POST('/api/settings/email', { body })),
    meta: { silent: true, successMessage: 'Email de acceso actualizado.' },
    onSuccess: (profile) => {
      qc.setQueryData(settingsKeys.profile, profile)
      void qc.invalidateQueries({ queryKey: sessionKey })
      void qc.invalidateQueries({ queryKey: teamKey })
    },
  })
}

export function useRevokeOtherSessions() {
  return useMutation({
    mutationFn: () => unwrap(api.POST('/api/settings/sessions/revoke')),
    meta: { successMessage: 'Cerraste la sesión en los demás dispositivos.' },
  })
}

// ── Negocio ──────────────────────────────────────────────────────────────────

export const businessSettingsQuery = queryOptions({
  queryKey: settingsKeys.business,
  queryFn: () => unwrap(api.GET('/api/settings/business')),
})

export function useUpdateBusiness() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: Schemas['SettingsBusinessUpdateRequest']) => unwrap(api.PUT('/api/settings/business', { body })),
    meta: { silent: true, successMessage: 'Datos del negocio guardados.' },
    onSuccess: (business) => {
      qc.setQueryData(settingsKeys.business, business)
      // Nombre y moneda se muestran en toda la app (topbar, importes)
      void qc.invalidateQueries({ queryKey: sessionKey })
    },
  })
}

export function useUploadLogo() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (file: File) => unwrap(api.POST('/api/settings/business/logo', fileBody(file))),
    meta: { successMessage: 'Logo actualizado.' },
    onSuccess: () => void qc.invalidateQueries({ queryKey: settingsKeys.business }),
  })
}

export function useDeleteLogo() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: () => unwrap(api.DELETE('/api/settings/business/logo')),
    meta: { successMessage: 'Logo eliminado.' },
    onSuccess: () => void qc.invalidateQueries({ queryKey: settingsKeys.business }),
  })
}

// ── Equipo ───────────────────────────────────────────────────────────────────

export const teamQuery = queryOptions({
  queryKey: teamKey,
  queryFn: () => unwrap(api.GET('/api/team')),
})

export function useInviteMember() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: Schemas['TeamInviteRequest']) => unwrap(api.POST('/api/team/invitations', { body })),
    meta: { silent: true },
    onSuccess: () => void qc.invalidateQueries({ queryKey: teamKey }),
  })
}

export function useRegenerateInvitation() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) =>
      unwrap(api.POST('/api/team/invitations/{id}/regenerate', { params: { path: { id } } })),
    onSuccess: () => void qc.invalidateQueries({ queryKey: teamKey }),
  })
}

export function useCancelInvitation() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => unwrap(api.DELETE('/api/team/invitations/{id}', { params: { path: { id } } })),
    meta: { successMessage: 'Invitación cancelada. El enlace ya no funciona.' },
    onSuccess: () => void qc.invalidateQueries({ queryKey: teamKey }),
  })
}

export function useUpdateMemberRole() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ userId, role }: { userId: string; role: UserRole }) =>
      unwrap(api.PUT('/api/team/members/{userId}/role', { params: { path: { userId } }, body: { role } })),
    meta: { successMessage: 'Rol actualizado.' },
    onSuccess: () => void qc.invalidateQueries({ queryKey: teamKey }),
  })
}

export function useRemoveMember() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (userId: string) => unwrap(api.DELETE('/api/team/members/{userId}', { params: { path: { userId } } })),
    meta: { successMessage: 'Usuario quitado del equipo.' },
    onSuccess: () => void qc.invalidateQueries({ queryKey: teamKey }),
  })
}

// ── Notificaciones por email ─────────────────────────────────────────────────

export const notificationEmailsQuery = queryOptions({
  queryKey: settingsKeys.notificationEmails,
  queryFn: () => unwrap(api.GET('/api/settings/notification-emails')),
})

export function useAddNotificationEmail() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (email: string) => unwrap(api.POST('/api/settings/notification-emails', { body: { email } })),
    meta: { silent: true, successMessage: 'Email añadido.' },
    onSuccess: () => void qc.invalidateQueries({ queryKey: settingsKeys.notificationEmails }),
  })
}

export function useToggleNotificationEmail() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, isActive }: { id: number; isActive: boolean }) =>
      unwrap(api.PATCH('/api/settings/notification-emails/{id}', { params: { path: { id } }, body: { isActive } })),
    // Actualización optimista: el interruptor responde al instante
    onMutate: async ({ id, isActive }) => {
      await qc.cancelQueries({ queryKey: settingsKeys.notificationEmails })
      const previous = qc.getQueryData<NotificationEmails>(settingsKeys.notificationEmails)
      if (previous)
        qc.setQueryData<NotificationEmails>(settingsKeys.notificationEmails, {
          ...previous,
          items: previous.items.map((e) => (e.id === id ? { ...e, isActive } : e)),
        })
      return { previous }
    },
    onError: (_error, _vars, context) => {
      if (context?.previous) qc.setQueryData(settingsKeys.notificationEmails, context.previous)
    },
    onSettled: () => void qc.invalidateQueries({ queryKey: settingsKeys.notificationEmails }),
  })
}

export function useDeleteNotificationEmail() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) =>
      unwrap(api.DELETE('/api/settings/notification-emails/{id}', { params: { path: { id } } })),
    meta: { successMessage: 'Email eliminado.' },
    onSuccess: () => void qc.invalidateQueries({ queryKey: settingsKeys.notificationEmails }),
  })
}

// ── Invitación pública ───────────────────────────────────────────────────────

export const invitationQuery = (token: string) =>
  queryOptions({
    queryKey: invitationKey(token),
    queryFn: () => unwrap(api.GET('/api/invitations/{token}', { params: { path: { token } } })),
    staleTime: 60_000,
  })

/** Acepta la invitación; la respuesta es la nueva sesión (como tras iniciar sesión). */
export function useAcceptInvitation(token: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: async (body: Schemas['InvitationAcceptRequest']): Promise<Session> => {
      await ensureCsrf()
      return unwrap(api.POST('/api/invitations/{token}/accept', { params: { path: { token } }, body }))
    },
    meta: { silent: true },
    onSuccess: (session) => {
      // El token CSRF y el negocio cambian: se descarta la caché anterior
      qc.clear()
      qc.setQueryData(sessionKey, session)
    },
  })
}
