import { queryOptions, useMutation, useQueryClient } from '@tanstack/react-query'
import { api, unwrap, type Schemas } from '@/lib/api/client'

export type SellerProfileResponse = Schemas['SellerProfileResponse']
export type SellerProfile = Schemas['SellerProfileDto']
export type SellerProfileTemplate = Schemas['SellerProfileTemplateDto']
export type SellerProfileUpdate = Schemas['SellerProfileUpdateRequest']
export type SellerRules = Schemas['SellerRulesDto']
export type SellerRulesUpdate = Schemas['SellerRulesUpdateRequest']
export type QuickReply = Schemas['SellerRulesQuickReplyDto']
export type QuickReplyUpsert = Schemas['SellerRulesQuickReplyUpsertRequest']
export type SellerTestResponse = Schemas['SellerTestResponse']

/** Claves jerárquicas del módulo (raíz propia para no interferir con otras pestañas de Mi vendedor). */
export const sellerProfileKeys = {
  all: ['seller-profile'] as const,
  profile: () => [...sellerProfileKeys.all, 'profile'] as const,
  rules: () => [...sellerProfileKeys.all, 'rules'] as const,
  quickReplies: () => [...sellerProfileKeys.all, 'quick-replies'] as const,
}

// ── Personalidad ────────────────────────────────────────────────────────────

export const sellerProfileQuery = queryOptions({
  queryKey: sellerProfileKeys.profile(),
  queryFn: () => unwrap(api.GET('/api/seller/profile')),
})

export function useUpdateSellerProfile() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: SellerProfileUpdate) => unwrap(api.PUT('/api/seller/profile', { body })),
    meta: { successMessage: 'Personalidad guardada. Tu vendedor ya usa la nueva configuración.' },
    onSuccess: (data) => qc.setQueryData(sellerProfileKeys.profile(), data),
  })
}

// ── Reglas y políticas ──────────────────────────────────────────────────────

export const sellerRulesQuery = queryOptions({
  queryKey: sellerProfileKeys.rules(),
  queryFn: () => unwrap(api.GET('/api/seller/rules')),
})

export function useUpdateSellerRules() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: SellerRulesUpdate) => unwrap(api.PUT('/api/seller/rules', { body })),
    meta: { successMessage: 'Reglas y políticas guardadas.' },
    onSuccess: (data) => qc.setQueryData(sellerProfileKeys.rules(), data),
  })
}

// ── Respuestas fijas ────────────────────────────────────────────────────────

export const quickRepliesQuery = queryOptions({
  queryKey: sellerProfileKeys.quickReplies(),
  queryFn: () => unwrap(api.GET('/api/seller/quick-replies')),
})

export function useSaveQuickReply() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, body }: { id?: number; body: QuickReplyUpsert }) =>
      id == null
        ? unwrap(api.POST('/api/seller/quick-replies', { body }))
        : unwrap(api.PUT('/api/seller/quick-replies/{id}', { params: { path: { id } }, body })),
    meta: { successMessage: 'Respuesta fija guardada.' },
    onSuccess: () => qc.invalidateQueries({ queryKey: sellerProfileKeys.quickReplies() }),
  })
}

export function useToggleQuickReply() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (reply: QuickReply) =>
      unwrap(
        api.PUT('/api/seller/quick-replies/{id}', {
          params: { path: { id: reply.id } },
          body: {
            name: reply.name,
            triggerPhrases: reply.triggerPhrases,
            response: reply.response,
            priority: reply.priority,
            isActive: !reply.isActive,
          },
        }),
      ),
    onSuccess: () => qc.invalidateQueries({ queryKey: sellerProfileKeys.quickReplies() }),
  })
}

export function useDeleteQuickReply() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => unwrap(api.DELETE('/api/seller/quick-replies/{id}', { params: { path: { id } } })),
    meta: { successMessage: 'Respuesta fija eliminada.' },
    onSuccess: () => qc.invalidateQueries({ queryKey: sellerProfileKeys.quickReplies() }),
  })
}

// ── Probar vendedor ─────────────────────────────────────────────────────────

/** Vista previa del bot (no persiste nada). Los errores se muestran dentro del chat, no como toast. */
export function useSellerTest() {
  return useMutation({
    mutationFn: (message: string) => unwrap(api.POST('/api/seller/test', { body: { message } })),
    meta: { silent: true },
  })
}
