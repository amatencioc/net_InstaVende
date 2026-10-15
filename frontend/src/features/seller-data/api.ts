import { keepPreviousData, queryOptions, useMutation, useQueryClient, type QueryKey } from '@tanstack/react-query'
import { api, unwrap, type Schemas } from '@/lib/api/client'

// Datos que el vendedor IA usa como fuente de verdad: base de conocimiento, zonas de entrega y pagos.
// Toda escritura invalida la raíz de su recurso; los interruptores y el orden son optimistas.

export type KnowledgeEntry = Schemas['KnowledgeEntryDto']
export type KnowledgeCategory = Schemas['KnowledgeCategory']
export type KnowledgeList = Schemas['KnowledgeListDto']
export type KnowledgeUpsert = Schemas['KnowledgeUpsertRequest']
export type KnowledgeImportResult = Schemas['KnowledgeImportResultDto']
export type DeliveryZone = Schemas['DeliveryZoneDto']
export type DeliveryZoneUpsert = Schemas['DeliveryZoneUpsertRequest']
export type PaymentMethod = Schemas['PaymentMethodDto']
export type PaymentMethodType = Schemas['PaymentMethodType']
export type PaymentMethodUpsert = Schemas['PaymentMethodUpsertRequest']
export type PaymentImage = Schemas['PaymentImageDto']

export interface KnowledgeListParams {
  category?: KnowledgeCategory
  search?: string
}

// ── Claves ───────────────────────────────────────────────────────────────────

export const knowledgeKeys = {
  all: ['knowledge'] as const,
  list: (params: KnowledgeListParams) => ['knowledge', 'list', params] as const,
}
export const deliveryZoneKeys = { all: ['delivery-zones'] as const, list: () => ['delivery-zones', 'list'] as const }
export const paymentMethodKeys = { all: ['payment-methods'] as const, list: () => ['payment-methods', 'list'] as const }
export const paymentImageKeys = { all: ['payment-images'] as const, list: () => ['payment-images', 'list'] as const }

// ── Consultas ────────────────────────────────────────────────────────────────

export const knowledgeListQuery = (params: KnowledgeListParams) =>
  queryOptions({
    queryKey: knowledgeKeys.list(params),
    queryFn: () =>
      unwrap(
        api.GET('/api/knowledge', {
          params: { query: { category: params.category, search: params.search || undefined } },
        }),
      ),
    placeholderData: keepPreviousData,
  })

export const deliveryZonesQuery = queryOptions({
  queryKey: deliveryZoneKeys.list(),
  queryFn: () => unwrap(api.GET('/api/delivery-zones')),
})

export const paymentMethodsQuery = queryOptions({
  queryKey: paymentMethodKeys.list(),
  queryFn: () => unwrap(api.GET('/api/payment-methods')),
})

export const paymentImagesQuery = queryOptions({
  queryKey: paymentImageKeys.list(),
  queryFn: () => unwrap(api.GET('/api/payment-images')),
})

// ── Utilidades de actualización optimista ────────────────────────────────────

type Snapshot = [QueryKey, unknown][]

/**
 * Aplica `update` a todas las consultas bajo `root` y devuelve una copia para revertir si la petición falla.
 * Las mutaciones que la usan invalidan `root` al terminar para quedar alineadas con el servidor.
 */
function useOptimisticCache<TData>(root: QueryKey) {
  const qc = useQueryClient()
  return {
    async apply(update: (data: TData) => TData): Promise<Snapshot> {
      await qc.cancelQueries({ queryKey: root })
      const snapshot = qc.getQueriesData<TData>({ queryKey: root })
      qc.setQueriesData<TData>({ queryKey: root }, (data) => (data === undefined ? data : update(data)))
      return snapshot
    },
    rollback(snapshot: Snapshot | undefined) {
      snapshot?.forEach(([key, data]) => qc.setQueryData(key, data))
    },
    invalidate: () => qc.invalidateQueries({ queryKey: root }),
  }
}

function reorderById<T extends { id: number }>(items: T[], ids: number[]): T[] {
  const byId = new Map(items.map((item) => [item.id, item]))
  const ordered = ids.map((id) => byId.get(id)).filter((item): item is T => item !== undefined)
  const rest = items.filter((item) => !ids.includes(item.id))
  return [...ordered, ...rest].map((item, index) => ({ ...item, sortOrder: index }))
}

// ── Base de conocimiento ─────────────────────────────────────────────────────

export function useSaveKnowledge() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, body }: { id?: number; body: KnowledgeUpsert }) =>
      id
        ? unwrap(api.PUT('/api/knowledge/{id}', { params: { path: { id } }, body }))
        : unwrap(api.POST('/api/knowledge', { body })),
    meta: { silent: true }, // el diálogo muestra el error en línea
    onSettled: () => qc.invalidateQueries({ queryKey: knowledgeKeys.all }),
  })
}

export function useDeleteKnowledge() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => unwrap(api.DELETE('/api/knowledge/{id}', { params: { path: { id } } })),
    meta: { successMessage: 'Tema eliminado.' },
    onSettled: () => qc.invalidateQueries({ queryKey: knowledgeKeys.all }),
  })
}

export function useToggleKnowledgeFavorite() {
  const optimistic = useOptimisticCache<KnowledgeList>(knowledgeKeys.all)
  return useMutation({
    mutationFn: ({ id, isFavorite }: { id: number; isFavorite: boolean }) =>
      unwrap(api.POST('/api/knowledge/{id}/favorite', { params: { path: { id } }, body: { isFavorite } })),
    onMutate: ({ id, isFavorite }) =>
      optimistic.apply((list) => ({
        ...list,
        items: list.items.map((item) => (item.id === id ? { ...item, isFavorite } : item)),
      })),
    onError: (_error, _vars, snapshot) => optimistic.rollback(snapshot),
    onSettled: () => optimistic.invalidate(),
  })
}

export function useImportKnowledge() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (file: File) => {
      const form = new FormData()
      form.append('file', file)
      return unwrap(api.POST('/api/knowledge/import', { body: {}, bodySerializer: () => form }))
    },
    meta: { silent: true },
    onSuccess: (result) => {
      if (result.created > 0) void qc.invalidateQueries({ queryKey: knowledgeKeys.all })
    },
  })
}

// ── Zonas de entrega ─────────────────────────────────────────────────────────

export function useSaveDeliveryZone() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, body }: { id?: number; body: DeliveryZoneUpsert }) =>
      id
        ? unwrap(api.PUT('/api/delivery-zones/{id}', { params: { path: { id } }, body }))
        : unwrap(api.POST('/api/delivery-zones', { body })),
    meta: { silent: true },
    onSettled: () => qc.invalidateQueries({ queryKey: deliveryZoneKeys.all }),
  })
}

export function useDeleteDeliveryZone() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => unwrap(api.DELETE('/api/delivery-zones/{id}', { params: { path: { id } } })),
    meta: { successMessage: 'Zona eliminada.' },
    onSettled: () => qc.invalidateQueries({ queryKey: deliveryZoneKeys.all }),
  })
}

export function useToggleDeliveryZone() {
  const optimistic = useOptimisticCache<DeliveryZone[]>(deliveryZoneKeys.all)
  return useMutation({
    mutationFn: ({ id, isActive }: { id: number; isActive: boolean }) =>
      unwrap(api.POST('/api/delivery-zones/{id}/toggle', { params: { path: { id } }, body: { isActive } })),
    onMutate: ({ id, isActive }) =>
      optimistic.apply((zones) => zones.map((z) => (z.id === id ? { ...z, isActive } : z))),
    onError: (_error, _vars, snapshot) => optimistic.rollback(snapshot),
    onSettled: () => optimistic.invalidate(),
  })
}

export function useReorderDeliveryZones() {
  const optimistic = useOptimisticCache<DeliveryZone[]>(deliveryZoneKeys.all)
  return useMutation({
    mutationFn: (ids: number[]) => unwrap(api.PUT('/api/delivery-zones/order', { body: { ids } })),
    onMutate: (ids) => optimistic.apply((zones) => reorderById(zones, ids)),
    onError: (_error, _vars, snapshot) => optimistic.rollback(snapshot),
    onSettled: () => optimistic.invalidate(),
  })
}

// ── Métodos de pago ──────────────────────────────────────────────────────────

export function useSavePaymentMethod() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, body }: { id?: number; body: PaymentMethodUpsert }) =>
      id
        ? unwrap(api.PUT('/api/payment-methods/{id}', { params: { path: { id } }, body }))
        : unwrap(api.POST('/api/payment-methods', { body })),
    meta: { silent: true },
    onSettled: () => qc.invalidateQueries({ queryKey: paymentMethodKeys.all }),
  })
}

export function useDeletePaymentMethod() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => unwrap(api.DELETE('/api/payment-methods/{id}', { params: { path: { id } } })),
    meta: { successMessage: 'Método de pago eliminado.' },
    onSettled: () => qc.invalidateQueries({ queryKey: paymentMethodKeys.all }),
  })
}

export function useTogglePaymentMethod() {
  const optimistic = useOptimisticCache<PaymentMethod[]>(paymentMethodKeys.all)
  return useMutation({
    mutationFn: ({ id, isActive }: { id: number; isActive: boolean }) =>
      unwrap(api.POST('/api/payment-methods/{id}/toggle', { params: { path: { id } }, body: { isActive } })),
    onMutate: ({ id, isActive }) =>
      optimistic.apply((methods) => methods.map((m) => (m.id === id ? { ...m, isActive } : m))),
    onError: (_error, _vars, snapshot) => optimistic.rollback(snapshot),
    onSettled: () => optimistic.invalidate(),
  })
}

export function useReorderPaymentMethods() {
  const optimistic = useOptimisticCache<PaymentMethod[]>(paymentMethodKeys.all)
  return useMutation({
    mutationFn: (ids: number[]) => unwrap(api.PUT('/api/payment-methods/order', { body: { ids } })),
    onMutate: (ids) => optimistic.apply((methods) => reorderById(methods, ids)),
    onError: (_error, _vars, snapshot) => optimistic.rollback(snapshot),
    onSettled: () => optimistic.invalidate(),
  })
}

// ── Imágenes de pago ─────────────────────────────────────────────────────────

export function useUploadPaymentImage() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (file: File) => {
      const form = new FormData()
      form.append('file', file)
      return unwrap(api.POST('/api/payment-images', { body: {}, bodySerializer: () => form }))
    },
    onSettled: () => qc.invalidateQueries({ queryKey: paymentImageKeys.all }),
  })
}

export function useDeletePaymentImage() {
  const optimistic = useOptimisticCache<PaymentImage[]>(paymentImageKeys.all)
  return useMutation({
    mutationFn: (id: number) => unwrap(api.DELETE('/api/payment-images/{id}', { params: { path: { id } } })),
    meta: { successMessage: 'Imagen eliminada.' },
    onMutate: (id) => optimistic.apply((images) => images.filter((i) => i.id !== id)),
    onError: (_error, _vars, snapshot) => optimistic.rollback(snapshot),
    onSettled: () => optimistic.invalidate(),
  })
}
