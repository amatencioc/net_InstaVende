import {
  infiniteQueryOptions,
  queryOptions,
  useMutation,
  useQueryClient,
  type InfiniteData,
  type QueryClient,
} from '@tanstack/react-query'
import { toast } from 'sonner'
import { api, unwrap, type Schemas } from '@/lib/api/client'

// ── Tipos ────────────────────────────────────────────────────────────────────

export type InboxConversation = Schemas['InboxConversationDto']
export type InboxConversationDetail = Schemas['InboxConversationDetailDto']
export type InboxCounts = Schemas['InboxCountsDto']
export type InboxMessage = Schemas['InboxMessageDto']
export type InboxMessagePage = Schemas['InboxMessagePageDto']
export type InboxStatusFilter = Schemas['InboxStatusFilter']
export type InboxAgent = Schemas['InboxAgentDto']
export type InboxLabelRef = Schemas['InboxLabelRefDto']
export type ConversationLabel = Schemas['ConversationLabelDto']
export type ConversationLabelInput = Schemas['ConversationLabelUpsertRequest']
export type ConversationStatus = Schemas['ConversationStatus']
export type ChannelType = Schemas['ChannelType']

/** Mensaje en caché: los enviados con actualización optimista llevan `clientId` y un id negativo temporal. */
export type ChatMessage = InboxMessage & { clientId?: string; localError?: string }

export interface ConversationFilters {
  status: InboxStatusFilter
  channel?: ChannelType
  labelId?: number
  search?: string
}

type MessagesData = InfiniteData<{ items: ChatMessage[]; hasMore: boolean }, number | undefined>
type ConversationsData = InfiniteData<Schemas['PagedResultOfInboxConversationDto'], number>

// ── Claves ───────────────────────────────────────────────────────────────────

export const inboxKeys = {
  all: ['inbox'] as const,
  conversations: () => [...inboxKeys.all, 'conversations'] as const,
  conversationList: (filters: ConversationFilters) => [...inboxKeys.conversations(), filters] as const,
  counts: () => [...inboxKeys.all, 'counts'] as const,
  countsFor: (filters: Pick<ConversationFilters, 'channel' | 'labelId'>) =>
    [...inboxKeys.counts(), filters] as const,
  detail: (id: number) => [...inboxKeys.all, 'detail', id] as const,
  messages: (id: number) => [...inboxKeys.all, 'messages', id] as const,
}

export const labelKeys = {
  all: ['conversation-labels'] as const,
}

// ── Consultas ────────────────────────────────────────────────────────────────

const CONVERSATION_PAGE_SIZE = 30
const MESSAGE_PAGE_SIZE = 50

export function conversationsQuery(filters: ConversationFilters) {
  return infiniteQueryOptions({
    queryKey: inboxKeys.conversationList(filters),
    queryFn: ({ pageParam, signal }) =>
      unwrap(
        api.GET('/api/conversations', {
          params: {
            query: {
              status: filters.status,
              channel: filters.channel,
              labelId: filters.labelId,
              search: filters.search || undefined,
              page: pageParam,
              pageSize: CONVERSATION_PAGE_SIZE,
            },
          },
          signal,
        }),
      ),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.page < (last.totalPages ?? 0) ? last.page + 1 : undefined),
    staleTime: 15_000,
  })
}

export function countsQuery(filters: Pick<ConversationFilters, 'channel' | 'labelId'>) {
  return queryOptions({
    queryKey: inboxKeys.countsFor(filters),
    queryFn: ({ signal }) =>
      unwrap(api.GET('/api/conversations/counts', { params: { query: filters }, signal })),
    staleTime: 15_000,
  })
}

export function conversationDetailQuery(id: number) {
  return queryOptions({
    queryKey: inboxKeys.detail(id),
    queryFn: ({ signal }) => unwrap(api.GET('/api/conversations/{id}', { params: { path: { id } }, signal })),
  })
}

/**
 * Historial de mensajes. pages[0] es el bloque más reciente; las páginas siguientes son anteriores.
 * Cada página está en orden cronológico ascendente.
 */
export function messagesQuery(id: number) {
  return infiniteQueryOptions({
    queryKey: inboxKeys.messages(id),
    queryFn: async ({ pageParam, signal }): Promise<{ items: ChatMessage[]; hasMore: boolean }> =>
      unwrap(
        api.GET('/api/conversations/{id}/messages', {
          params: { path: { id }, query: { before: pageParam, limit: MESSAGE_PAGE_SIZE } },
          signal,
        }),
      ),
    initialPageParam: undefined as number | undefined,
    getNextPageParam: (last) => (last.hasMore ? last.items[0]?.id : undefined),
    // El tiempo real mantiene la conversación al día; se refresca al volver a la pestaña
    staleTime: 60_000,
  })
}

export const labelsQuery = queryOptions({
  queryKey: labelKeys.all,
  queryFn: ({ signal }) => unwrap(api.GET('/api/conversation-labels', { signal })),
  staleTime: 5 * 60_000,
})

// ── Caché de mensajes ────────────────────────────────────────────────────────

/** Mensajes en orden cronológico ascendente a partir de la caché paginada. */
export function flattenMessages(data: { pages: { items: ChatMessage[] }[] } | undefined): ChatMessage[] {
  if (!data) return []
  const out: ChatMessage[] = []
  for (let i = data.pages.length - 1; i >= 0; i--) out.push(...(data.pages[i]?.items ?? []))
  return out
}

function updateMessages(
  qc: QueryClient,
  conversationId: number,
  fn: (items: ChatMessage[], pageIndex: number) => ChatMessage[],
) {
  qc.setQueryData<MessagesData>(inboxKeys.messages(conversationId), (data) =>
    data ? { ...data, pages: data.pages.map((p, i) => ({ ...p, items: fn(p.items, i) })) } : data,
  )
}

/**
 * Añade un mensaje al final de la conversación en caché o lo actualiza si ya existe.
 * Si hay un mensaje optimista equivalente (mismo `clientId`, o saliente humano con el mismo texto) lo reemplaza.
 */
export function upsertMessage(
  qc: QueryClient,
  conversationId: number,
  message: ChatMessage,
  replaceClientId?: string,
) {
  qc.setQueryData<MessagesData>(inboxKeys.messages(conversationId), (data) => {
    if (!data) return data
    const has = (pred: (m: ChatMessage) => boolean) => data.pages.some((p) => p.items.some(pred))
    const exists = has((m) => m.id === message.id)
    let placeholder = replaceClientId
    if (!placeholder && !exists && message.direction === 'Outbound' && !message.sentByBot) {
      placeholder = flattenMessages(data).find(
        (m) => m.clientId && m.id < 0 && m.content === message.content,
      )?.clientId
    }
    const hasPlaceholder = placeholder !== undefined && has((m) => m.clientId === placeholder)

    const pages = data.pages.map((p) => ({
      ...p,
      items: p.items.flatMap((m): ChatMessage[] => {
        if (m.id === message.id) {
          // Un evento tardío "Pending" no debe pisar un estado final ya conocido
          const deliveryStatus =
            message.deliveryStatus === 'Pending' ? m.deliveryStatus : message.deliveryStatus
          return [{ ...m, ...message, deliveryStatus }]
        }
        if (hasPlaceholder && m.clientId === placeholder)
          return exists ? [] : [{ ...message, clientId: undefined }]
        return [m]
      }),
    }))
    if (!exists && !hasPlaceholder && pages[0])
      pages[0] = { ...pages[0], items: [...pages[0].items, message] }
    return { ...data, pages }
  })
}

export function setMessageStatus(
  qc: QueryClient,
  conversationId: number,
  messageId: number,
  status: InboxMessage['deliveryStatus'],
) {
  updateMessages(qc, conversationId, (items) =>
    items.some((m) => m.id === messageId)
      ? items.map((m) => (m.id === messageId ? { ...m, deliveryStatus: status } : m))
      : items,
  )
}

function removeMessage(qc: QueryClient, conversationId: number, predicate: (m: ChatMessage) => boolean) {
  updateMessages(qc, conversationId, (items) =>
    items.some(predicate) ? items.filter((m) => !predicate(m)) : items,
  )
}

/** Pone a cero el contador de no leídos en las listas en caché (sin esperar al servidor). */
function clearUnreadInLists(qc: QueryClient, conversationId: number) {
  qc.setQueriesData<ConversationsData>({ queryKey: inboxKeys.conversations() }, (data) =>
    data
      ? {
          ...data,
          pages: data.pages.map((p) => ({
            ...p,
            items: p.items.map((c) =>
              c.id === conversationId && c.unreadCount > 0 ? { ...c, unreadCount: 0 } : c,
            ),
          })),
        }
      : data,
  )
}

function patchConversationInLists(
  qc: QueryClient,
  conversationId: number,
  patch: Partial<InboxConversation>,
) {
  qc.setQueriesData<ConversationsData>({ queryKey: inboxKeys.conversations() }, (data) =>
    data
      ? {
          ...data,
          pages: data.pages.map((p) => ({
            ...p,
            items: p.items.map((c) => (c.id === conversationId ? { ...c, ...patch } : c)),
          })),
        }
      : data,
  )
}

export function invalidateInboxLists(qc: QueryClient) {
  void qc.invalidateQueries({ queryKey: inboxKeys.conversations() })
  void qc.invalidateQueries({ queryKey: inboxKeys.counts() })
}

// ── Mutaciones ───────────────────────────────────────────────────────────────

export function useMarkRead() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) =>
      unwrap(api.POST('/api/conversations/{id}/read', { params: { path: { id } } })),
    meta: { silent: true },
    onMutate: (id) => {
      clearUnreadInLists(qc, id)
      qc.setQueryData<InboxConversationDetail>(inboxKeys.detail(id), (d) =>
        d ? { ...d, unreadCount: 0 } : d,
      )
    },
    onSettled: () => void qc.invalidateQueries({ queryKey: inboxKeys.counts() }),
  })
}

let clientSeq = 0

/** Envío manual con actualización optimista. Los fallos del canal llegan como deliveryStatus = Failed. */
export function useSendMessage(conversationId: number) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ text }: { text: string; clientId: string }) =>
      unwrap(
        api.POST('/api/conversations/{id}/messages', {
          params: { path: { id: conversationId } },
          body: { text },
        }),
      ),
    meta: { silent: true }, // el error se muestra en la propia burbuja con opción de reintentar
    onMutate: ({ text, clientId }) => {
      const optimistic: ChatMessage = {
        id: -++clientSeq,
        clientId,
        conversationId,
        direction: 'Outbound',
        content: text,
        sentByBot: false,
        deliveryStatus: 'Pending',
        sentAt: new Date().toISOString(),
        mediaUrl: null,
        mediaType: null,
      }
      upsertMessage(qc, conversationId, optimistic)
      patchConversationInLists(qc, conversationId, {
        lastMessagePreview: text,
        lastMessageAt: optimistic.sentAt,
      })
    },
    onSuccess: (message, { clientId }) => {
      upsertMessage(qc, conversationId, message, clientId)
    },
    onError: (error, { clientId }) => {
      updateMessages(qc, conversationId, (items) =>
        items.map((m) =>
          m.clientId === clientId
            ? {
                ...m,
                deliveryStatus: 'Failed',
                localError: error instanceof Error ? error.message : 'No se pudo enviar.',
              }
            : m,
        ),
      )
    },
    onSettled: () => {
      void qc.invalidateQueries({ queryKey: inboxKeys.detail(conversationId) })
      invalidateInboxLists(qc)
    },
  })
}

/** Reintenta un mensaje fallido: en el servidor si llegó a guardarse, o reenviándolo si nunca llegó. */
export function useRetryMessage(conversationId: number) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: async (message: ChatMessage) => {
      if (message.id > 0) {
        return unwrap(
          api.POST('/api/conversations/{id}/messages/{messageId}/retry', {
            params: { path: { id: conversationId, messageId: message.id } },
          }),
        )
      }
      return unwrap(
        api.POST('/api/conversations/{id}/messages', {
          params: { path: { id: conversationId } },
          body: { text: message.content },
        }),
      )
    },
    onMutate: (message) => {
      updateMessages(qc, conversationId, (items) =>
        items.map((m) =>
          m.id === message.id ? { ...m, deliveryStatus: 'Pending', localError: undefined } : m,
        ),
      )
    },
    onSuccess: (sent, original) => {
      removeMessage(qc, conversationId, (m) => m.id === original.id)
      upsertMessage(qc, conversationId, sent)
    },
    onError: (error, original) => {
      updateMessages(qc, conversationId, (items) =>
        items.map((m) =>
          m.id === original.id
            ? {
                ...m,
                deliveryStatus: 'Failed',
                localError: error instanceof Error ? error.message : undefined,
              }
            : m,
        ),
      )
    },
    onSettled: () => invalidateInboxLists(qc),
  })
}

const statusSuccess: Record<ConversationStatus, string> = {
  HumanActive: 'Tomaste el control de la conversación.',
  BotActive: 'El vendedor IA vuelve a atender la conversación.',
  Resolved: 'Conversación resuelta.',
  WaitingHuman: 'Estado actualizado.',
}

export function useChangeStatus(conversationId: number) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (status: ConversationStatus) =>
      unwrap(
        api.POST('/api/conversations/{id}/status', {
          params: { path: { id: conversationId } },
          body: { status },
        }),
      ),
    onSuccess: (state) => {
      qc.setQueryData<InboxConversationDetail>(inboxKeys.detail(conversationId), (d) =>
        d
          ? { ...d, status: state.status, assignedAgent: state.assignedAgent, resolvedAt: state.resolvedAt }
          : d,
      )
      patchConversationInLists(qc, conversationId, {
        status: state.status,
        assignedAgent: state.assignedAgent,
      })
      toast.success(statusSuccess[state.status])
    },
    onSettled: () => {
      void qc.invalidateQueries({ queryKey: inboxKeys.detail(conversationId) })
      invalidateInboxLists(qc)
    },
  })
}

export function useSetConversationLabel(conversationId: number) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (label: InboxLabelRef | null) =>
      unwrap(
        api.PUT('/api/conversations/{id}/label', {
          params: { path: { id: conversationId } },
          body: { labelId: label?.id ?? null },
        }),
      ),
    onMutate: async (label) => {
      await qc.cancelQueries({ queryKey: inboxKeys.detail(conversationId) })
      const previous = qc.getQueryData<InboxConversationDetail>(inboxKeys.detail(conversationId))
      qc.setQueryData<InboxConversationDetail>(inboxKeys.detail(conversationId), (d) =>
        d ? { ...d, label } : d,
      )
      patchConversationInLists(qc, conversationId, { label })
      return { previous }
    },
    onError: (_e, _label, ctx) => {
      if (ctx?.previous) qc.setQueryData(inboxKeys.detail(conversationId), ctx.previous)
    },
    onSettled: () => {
      void qc.invalidateQueries({ queryKey: labelKeys.all })
      void qc.invalidateQueries({ queryKey: inboxKeys.detail(conversationId) })
      invalidateInboxLists(qc)
    },
  })
}

export function useSaveLabel() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, ...body }: ConversationLabelInput & { id?: number }) =>
      id
        ? unwrap(api.PUT('/api/conversation-labels/{id}', { params: { path: { id } }, body }))
        : unwrap(api.POST('/api/conversation-labels', { body })),
    meta: { silent: true }, // el formulario muestra el error en línea
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: labelKeys.all })
      // Los nombres/colores embebidos en conversaciones cambian
      void qc.invalidateQueries({ queryKey: inboxKeys.all })
    },
  })
}

export function useDeleteLabel() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) =>
      unwrap(api.DELETE('/api/conversation-labels/{id}', { params: { path: { id } } })),
    meta: { successMessage: 'Etiqueta eliminada.' },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: labelKeys.all })
      void qc.invalidateQueries({ queryKey: inboxKeys.all })
    },
  })
}
