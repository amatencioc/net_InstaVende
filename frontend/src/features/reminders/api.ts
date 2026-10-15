import { keepPreviousData, queryOptions, useMutation, useQueryClient } from '@tanstack/react-query'
import { api, unwrap, type Schemas } from '@/lib/api/client'

export type Reminder = Schemas['ReminderDto']
export type ReminderStatus = Schemas['ReminderStatus']
export type ReminderCounts = Schemas['ReminderCountsDto']
export type ReminderUpsert = Schemas['ReminderUpsertRequest']
export type ReminderContactOption = Schemas['ReminderContactOptionDto']
export type CustomerSegment = Schemas['CustomerSegment']
export type ReminderTemplateSegment = Schemas['ReminderTemplateSegmentDto']
export type ReminderTemplateItem = Schemas['ReminderTemplateItemDto']
export type ReminderTemplateSuggestion = Schemas['ReminderTemplateSuggestionDto']

export interface ReminderListParams {
  status?: ReminderStatus
  search?: string
  page?: number
}

const remindersKey = ['reminders'] as const
const templatesKey = ['reminder-templates'] as const

// ── Recordatorios programados ───────────────────────────────────────────────

export const remindersQueries = {
  all: remindersKey,
  list: (params: ReminderListParams) =>
    queryOptions({
      queryKey: [...remindersKey, 'list', params] as const,
      queryFn: () =>
        unwrap(
          api.GET('/api/reminders', {
            params: {
              query: { status: params.status, search: params.search || undefined, page: params.page },
            },
          }),
        ),
      placeholderData: keepPreviousData,
    }),
  counts: (search?: string) =>
    queryOptions({
      queryKey: [...remindersKey, 'counts', { search }] as const,
      queryFn: () =>
        unwrap(api.GET('/api/reminders/counts', { params: { query: { search: search || undefined } } })),
      placeholderData: keepPreviousData,
    }),
  contacts: (search: string) =>
    queryOptions({
      queryKey: [...remindersKey, 'contacts', search] as const,
      queryFn: () =>
        unwrap(api.GET('/api/reminders/contacts', { params: { query: { search: search || undefined } } })),
      staleTime: 60_000,
      placeholderData: keepPreviousData,
    }),
}

/** Crea o edita un recordatorio (con id → PUT). */
export function useSaveReminder() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, body }: { id?: number; body: ReminderUpsert }) =>
      id
        ? unwrap(api.PUT('/api/reminders/{id}', { params: { path: { id } }, body }))
        : unwrap(api.POST('/api/reminders', { body })),
    meta: { silent: true }, // el diálogo muestra el error en línea
    onSuccess: () => qc.invalidateQueries({ queryKey: remindersKey }),
  })
}

export function useCancelReminder() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => unwrap(api.POST('/api/reminders/{id}/cancel', { params: { path: { id } } })),
    meta: { successMessage: 'Recordatorio cancelado.' },
    onSettled: () => qc.invalidateQueries({ queryKey: remindersKey }),
  })
}

export function useDeleteReminder() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => unwrap(api.DELETE('/api/reminders/{id}', { params: { path: { id } } })),
    meta: { successMessage: 'Recordatorio eliminado.' },
    onSettled: () => qc.invalidateQueries({ queryKey: remindersKey }),
  })
}

// ── Plantillas por segmento ─────────────────────────────────────────────────

export const reminderTemplateQueries = {
  all: templatesKey,
  segments: () =>
    queryOptions({
      queryKey: [...templatesKey, 'segments'] as const,
      queryFn: () => unwrap(api.GET('/api/reminder-templates')),
    }),
  suggestions: (segment: CustomerSegment) =>
    queryOptions({
      queryKey: [...templatesKey, 'suggestions', segment] as const,
      queryFn: () =>
        unwrap(api.GET('/api/reminder-templates/suggestions', { params: { query: { segment } } })),
      staleTime: Infinity, // catálogo estático
    }),
}

export function useSaveTemplateSegment() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({
      segment,
      reminders,
    }: {
      segment: CustomerSegment
      reminders: Schemas['ReminderTemplateItemUpdate'][]
    }) =>
      unwrap(
        api.PUT('/api/reminder-templates/{segment}', { params: { path: { segment } }, body: { reminders } }),
      ),
    meta: { successMessage: 'Recordatorios guardados.' },
    onSuccess: (saved) => {
      qc.setQueryData(reminderTemplateQueries.segments().queryKey, (prev) =>
        prev?.map((s) => (s.segment === saved.segment ? saved : s)),
      )
    },
  })
}
