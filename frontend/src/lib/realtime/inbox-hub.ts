import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr'
import { useEffect, useRef, useSyncExternalStore } from 'react'
import { apiBaseUrl } from '@/lib/api/client'

// Enums serializados como texto por la API (ver InstaVende.Core/Enums)
export type MessageDirection = 'Inbound' | 'Outbound'
export type MessageDeliveryStatus = 'Pending' | 'Sent' | 'Failed'
export type ChannelType = 'WhatsApp' | 'FacebookMessenger' | 'Instagram'
export type ConversationStatus = 'BotActive' | 'WaitingHuman' | 'HumanActive' | 'Resolved'

/** Eventos que emite el servidor (ver InboxNotifier.cs). */
export interface InboxEvents {
  messageReceived: {
    conversationId: number
    message: {
      id: number
      content: string
      mediaUrl?: string | null
      mediaType?: string | null
      direction: MessageDirection
      sentByBot: boolean
      deliveryStatus: MessageDeliveryStatus
      sentAt: string
    }
    contactName?: string | null
    channel?: ChannelType | null
  }
  messageStatus: { conversationId: number; messageId: number; status: MessageDeliveryStatus }
  conversationUpdated: { conversationId: number; status: ConversationStatus }
  /** Avisos de negocio (pedido nuevo, cliente que pide un asesor). Ver BusinessEventDispatcher.cs. */
  businessEvent: {
    type: 'HandoffRequested' | 'OrderCreated'
    title: string
    message: string
    link?: string | null
    conversationId?: number | null
    orderId?: number | null
  }
}

let connection: HubConnection | null = null
const listeners = new Set<() => void>()
const notify = () => listeners.forEach((l) => l())

function getConnection(): HubConnection {
  connection ??= new HubConnectionBuilder()
    .withUrl(`${apiBaseUrl}/hubs/inbox`, { withCredentials: true })
    .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 30_000])
    .configureLogging(import.meta.env.DEV ? LogLevel.Warning : LogLevel.Error)
    .build()
  connection.onreconnecting(notify)
  connection.onreconnected(notify)
  connection.onclose(notify)
  return connection
}

let users = 0
let stopTimer: ReturnType<typeof setTimeout> | undefined
let retryTimer: ReturnType<typeof setTimeout> | undefined

/**
 * Registra un consumidor de la conexión y la inicia si hace falta. Devuelve la función que lo libera.
 * La parada se difiere: un desmontaje/montaje inmediato (StrictMode, cambio de layout) no corta
 * la conexión a mitad de la negociación.
 */
export function acquireInboxHub(): () => void {
  users++
  clearTimeout(stopTimer)
  void connect()
  let released = false
  return () => {
    if (released) return
    released = true
    users = Math.max(0, users - 1)
    if (users === 0) {
      clearTimeout(retryTimer)
      stopTimer = setTimeout(() => {
        if (users === 0) void connection?.stop().finally(notify)
      }, 1_000)
    }
  }
}

async function connect(): Promise<void> {
  const c = getConnection()
  if (c.state !== HubConnectionState.Disconnected || users === 0) return
  try {
    await c.start()
  } catch {
    // withAutomaticReconnect solo actúa tras una conexión exitosa: reintento manual
    clearTimeout(retryTimer)
    if (users > 0) retryTimer = setTimeout(() => void connect(), 5_000)
  } finally {
    notify()
  }
}

/** Estado de la conexión en tiempo real para mostrar un indicador en la UI. */
export function useInboxHubState(): HubConnectionState {
  return useSyncExternalStore(
    (cb) => {
      listeners.add(cb)
      return () => listeners.delete(cb)
    },
    () => connection?.state ?? HubConnectionState.Disconnected,
  )
}

/** Suscribe un manejador a un evento del hub mientras el componente esté montado. */
export function useInboxEvent<K extends keyof InboxEvents>(event: K, handler: (payload: InboxEvents[K]) => void): void {
  const ref = useRef(handler)
  useEffect(() => {
    ref.current = handler
  })
  useEffect(() => {
    const c = getConnection()
    const fn = (payload: InboxEvents[K]) => ref.current(payload)
    c.on(event, fn)
    return () => c.off(event, fn)
  }, [event])
}
