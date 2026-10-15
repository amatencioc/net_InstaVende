import { queryOptions, useMutation, useQueryClient, type QueryClient } from '@tanstack/react-query'
import { api, unwrap, type Schemas } from '@/lib/api/client'

export type ChannelsOverview = Schemas['ChannelsOverviewDto']
export type ChannelSummary = Schemas['ChannelSummaryDto']
export type ChannelType = Schemas['ChannelType']
export type ChannelStatus = Schemas['ChannelConnectionStatus']
export type ChannelServiceStatus = Schemas['ChannelServiceStatusDto']
export type ChannelMetaConfigRequest = Schemas['ChannelMetaConfigRequest']
export type ChannelTestResult = Schemas['ChannelTestResultDto']
export type ChannelActionResult = Schemas['ChannelActionResultDto']
export type WhatsAppSession = Schemas['WhatsAppSessionDto']

/** Estados de la sesión QR que informa el servidor (wa-client + "offline" si no responde). */
export type WhatsAppSessionState =
  'initializing' | 'qr' | 'authenticating' | 'connected' | 'disconnected' | 'offline'

export const channelKeys = {
  all: ['channels'] as const,
  overview: () => [...channelKeys.all, 'overview'] as const,
  whatsappSession: () => [...channelKeys.all, 'whatsapp-session'] as const,
}

/** Resumen de los tres canales y del servicio de sesiones QR. */
export const channelsOverviewQuery = queryOptions({
  queryKey: channelKeys.overview(),
  queryFn: ({ signal }) => unwrap(api.GET('/api/channels', { signal })),
})

export function findChannel(
  overview: ChannelsOverview | undefined,
  channel: ChannelType,
): ChannelSummary | undefined {
  return overview?.channels.find((c) => c.channel === channel)
}

/**
 * Frecuencia de consulta de la sesión QR: rápida mientras hay un código que escanear o se está
 * autenticando, lenta mientras Chrome arranca o el servicio no responde, y nula una vez conectada.
 * TanStack Query pausa el intervalo con la pestaña oculta (refetchIntervalInBackground = false).
 */
export function sessionPollInterval(session: WhatsAppSession | undefined): number | false {
  switch (session?.state as WhatsAppSessionState | undefined) {
    case 'connected':
      return false
    case 'qr':
    case 'authenticating':
      return 2_000
    default:
      return 5_000
  }
}

/**
 * Estado de la sesión QR. Envía el hash del QR que ya se muestra para que el servidor no reenvíe
 * la imagen si no cambió (`unchanged`), y en ese caso conserva la imagen anterior.
 */
export function whatsappSessionQuery(qc: QueryClient) {
  return queryOptions({
    queryKey: channelKeys.whatsappSession(),
    queryFn: async ({ signal }): Promise<WhatsAppSession> => {
      const previous = qc.getQueryData<WhatsAppSession>(channelKeys.whatsappSession())
      const qrHash = previous?.qrDataUrl && previous.qrHash ? previous.qrHash : undefined
      const next = await unwrap(
        api.GET('/api/channels/whatsapp/session', { params: { query: { qrHash } }, signal }),
      )
      if (next.unchanged && previous?.qrDataUrl) return { ...next, qrDataUrl: previous.qrDataUrl }
      return next
    },
    refetchInterval: (query) => sessionPollInterval(query.state.data),
    refetchIntervalInBackground: false,
    staleTime: 0,
    gcTime: 60_000,
    retry: 1,
  })
}

// ── Mutaciones ───────────────────────────────────────────────────────────────

function useInvalidateChannels() {
  const qc = useQueryClient()
  return () => qc.invalidateQueries({ queryKey: channelKeys.overview() })
}

export function useSaveMetaConfig(channel: ChannelType) {
  const invalidate = useInvalidateChannels()
  return useMutation({
    mutationFn: (body: ChannelMetaConfigRequest) =>
      unwrap(
        api.PUT('/api/channels/{channelType}/meta-config', {
          params: { path: { channelType: channel } },
          body,
        }),
      ),
    meta: { silent: true }, // el formulario muestra el error en línea
    onSuccess: invalidate,
  })
}

export function useRegenerateVerifyToken(channel: ChannelType) {
  const invalidate = useInvalidateChannels()
  return useMutation({
    mutationFn: () =>
      unwrap(
        api.POST('/api/channels/{channelType}/regenerate-verify-token', {
          params: { path: { channelType: channel } },
        }),
      ),
    meta: { successMessage: 'Token de verificación generado. Recuerda actualizarlo en Meta.' },
    onSuccess: invalidate,
  })
}

export function useTestChannel(channel: ChannelType) {
  const invalidate = useInvalidateChannels()
  return useMutation({
    mutationFn: () =>
      unwrap(api.POST('/api/channels/{channelType}/test', { params: { path: { channelType: channel } } })),
    onSuccess: invalidate, // la prueba de WhatsApp Cloud API actualiza el número visible
  })
}

export function useDisconnectChannel(channel: ChannelType) {
  const invalidate = useInvalidateChannels()
  return useMutation({
    mutationFn: () =>
      unwrap(api.DELETE('/api/channels/{channelType}', { params: { path: { channelType: channel } } })),
    onSuccess: invalidate,
  })
}

export function useDisconnectWhatsApp() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: () => unwrap(api.POST('/api/channels/whatsapp/disconnect')),
    onSuccess: async () => {
      qc.removeQueries({ queryKey: channelKeys.whatsappSession() })
      await qc.invalidateQueries({ queryKey: channelKeys.overview() })
    },
  })
}

/** Descarta la sesión y pide un QR nuevo: se olvida el hash anterior para recibir la imagen nueva. */
export function useResetWhatsApp() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: () => unwrap(api.POST('/api/channels/whatsapp/reset')),
    onSuccess: async () => {
      qc.removeQueries({ queryKey: channelKeys.whatsappSession() })
      await qc.invalidateQueries({ queryKey: channelKeys.all })
    },
  })
}

export function useRestartWhatsAppService() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: () => unwrap(api.POST('/api/channels/service/restart')),
    onSuccess: () => qc.invalidateQueries({ queryKey: channelKeys.all }),
  })
}
