import { HubConnectionState } from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'
import { useCallback, useEffect, useRef, useState, useSyncExternalStore } from 'react'
import {
  inboxKeys,
  invalidateInboxLists,
  setMessageStatus,
  upsertMessage,
  useMarkRead,
  type ChatMessage,
} from './api'
import { useInboxEvent, useInboxHubState } from '@/lib/realtime/inbox-hub'

/** true mientras la media query se cumpla (se actualiza al redimensionar). */
export function useMediaQuery(query: string): boolean {
  const subscribe = useCallback(
    (onChange: () => void) => {
      const mql = window.matchMedia(query)
      mql.addEventListener('change', onChange)
      return () => mql.removeEventListener('change', onChange)
    },
    [query],
  )
  return useSyncExternalStore(
    subscribe,
    () => window.matchMedia(query).matches,
    () => false,
  )
}

export function useDebouncedValue<T>(value: T, delay = 300): T {
  const [debounced, setDebounced] = useState(value)
  useEffect(() => {
    const t = setTimeout(() => setDebounced(value), delay)
    return () => clearTimeout(t)
  }, [value, delay])
  return debounced
}

/** Preferencia booleana del usuario guardada en localStorage (tolerante a almacenamiento bloqueado). */
export function useStoredFlag(key: string, initial: boolean): [boolean, (value: boolean) => void] {
  const [value, setValue] = useState(() => {
    try {
      const raw = localStorage.getItem(key)
      return raw === null ? initial : raw === '1'
    } catch {
      return initial
    }
  })
  const update = useCallback(
    (next: boolean) => {
      setValue(next)
      try {
        localStorage.setItem(key, next ? '1' : '0')
      } catch {
        // almacenamiento no disponible: la preferencia dura lo que la sesión
      }
    },
    [key],
  )
  return [value, update]
}

/**
 * Sincroniza la caché de la bandeja con los eventos en tiempo real:
 * añade mensajes a la conversación en caché, actualiza estados de entrega, refresca listas/contadores
 * y marca como leída la conversación abierta cuando llega un mensaje entrante.
 */
export function useInboxRealtime(activeConversationId: number | undefined) {
  const qc = useQueryClient()
  const { mutate: markRead } = useMarkRead()
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null)

  // Agrupa ráfagas de eventos en una sola recarga de listas y contadores
  const scheduleListRefresh = useCallback(() => {
    if (timer.current) return
    timer.current = setTimeout(() => {
      timer.current = null
      invalidateInboxLists(qc)
    }, 400)
  }, [qc])

  useEffect(
    () => () => {
      if (timer.current) clearTimeout(timer.current)
    },
    [],
  )

  useInboxEvent('messageReceived', (e) => {
    const message: ChatMessage = {
      ...e.message,
      conversationId: e.conversationId,
      mediaUrl: null,
      mediaType: null,
    }
    upsertMessage(qc, e.conversationId, message)
    scheduleListRefresh()
    if (
      e.conversationId === activeConversationId &&
      e.message.direction === 'Inbound' &&
      document.visibilityState === 'visible'
    ) {
      markRead(e.conversationId)
    }
  })

  useInboxEvent('messageStatus', (e) => setMessageStatus(qc, e.conversationId, e.messageId, e.status))

  useInboxEvent('conversationUpdated', (e) => {
    scheduleListRefresh()
    void qc.invalidateQueries({ queryKey: inboxKeys.detail(e.conversationId) })
  })

  // Tras una reconexión se pudieron perder eventos: se refresca todo lo de la bandeja
  const hubState = useInboxHubState()
  const previous = useRef(hubState)
  useEffect(() => {
    if (hubState === HubConnectionState.Connected && previous.current === HubConnectionState.Reconnecting) {
      void qc.invalidateQueries({ queryKey: inboxKeys.all })
    }
    previous.current = hubState
  }, [hubState, qc])
}
