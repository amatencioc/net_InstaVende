import { getRouteApi, useNavigate } from '@tanstack/react-router'
import { useCallback, useMemo } from 'react'
import type { ConversationFilters } from './api'
import type { InboxSearch } from './meta'

export const inboxRouteApi = getRouteApi('/_app/inbox')

/** Elimina claves vacías para mantener la URL limpia. */
function compact(search: InboxSearch): InboxSearch {
  const out: InboxSearch = {}
  if (search.status && search.status !== 'All') out.status = search.status
  if (search.channel) out.channel = search.channel
  if (search.label) out.label = search.label
  if (search.q?.trim()) out.q = search.q.trim()
  return out
}

/**
 * Filtros de la bandeja guardados en la URL (?status=&channel=&label=&q=).
 * Cambiarlos conserva la conversación abierta.
 */
export function useInboxFilters() {
  const search = inboxRouteApi.useSearch()
  const navigate = useNavigate()

  const setFilters = useCallback(
    (patch: Partial<InboxSearch>, options?: { replace?: boolean }) => {
      void navigate({
        to: '.',
        // Desde "." el tipo de prev es la unión de todas las rutas: se acota a la búsqueda de la bandeja
        search: (prev) => compact({ ...(prev as InboxSearch), ...patch }),
        replace: options?.replace,
      })
    },
    [navigate],
  )

  const filters = useMemo<ConversationFilters>(
    () => ({
      status: search.status ?? 'All',
      channel: search.channel,
      labelId: search.label,
      search: search.q,
    }),
    [search.status, search.channel, search.label, search.q],
  )

  return { search, filters, setFilters }
}
