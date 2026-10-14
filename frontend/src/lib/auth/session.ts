import { queryOptions, useMutation, useQueryClient, useSuspenseQuery } from '@tanstack/react-query'
import { api, ApiError, ensureCsrf, unwrap, type Schemas } from '@/lib/api/client'
import { formatMoney } from '@/lib/format'

export type Session = Schemas['SessionDto']
export type SessionBusiness = Schemas['SessionBusinessDto']

export const sessionKey = ['session'] as const

/** Sesión actual o null si no hay usuario autenticado. */
export const sessionQuery = queryOptions({
  queryKey: sessionKey,
  queryFn: async (): Promise<Session | null> => {
    await ensureCsrf()
    try {
      return await unwrap(api.GET('/api/auth/session'))
    } catch (e) {
      if (e instanceof ApiError && e.status === 401) return null
      throw e
    }
  },
  staleTime: 5 * 60_000,
})

/** Sesión garantizada (solo dentro de rutas protegidas por el layout _app). */
export function useSession(): Session & { business: SessionBusiness } {
  const { data } = useSuspenseQuery(sessionQuery)
  if (!data?.business) throw new Error('useSession() debe usarse dentro de una ruta autenticada con negocio.')
  return data as Session & { business: SessionBusiness }
}

/** Formatea importes con la moneda del negocio actual. */
export function useMoney(): (amount: number | null | undefined) => string {
  const { business } = useSession()
  return (amount) => formatMoney(amount, business.currencySymbol)
}

export function useIsAdmin(): boolean {
  return useSession().business.role === 'Admin'
}

function useSessionMutation<TVars>(fn: (vars: TVars) => Promise<Session>) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: async (vars: TVars) => {
      await ensureCsrf()
      return fn(vars)
    },
    meta: { silent: true }, // los formularios muestran el error en línea
    onSuccess: (session) => {
      // El token CSRF cambia con la identidad: se descarta el resto de la caché
      qc.clear()
      qc.setQueryData(sessionKey, session)
    },
  })
}

export function useLogin() {
  return useSessionMutation((body: Schemas['LoginRequest']) => unwrap(api.POST('/api/auth/login', { body })))
}

export function useRegister() {
  return useSessionMutation((body: Schemas['RegisterRequest']) => unwrap(api.POST('/api/auth/register', { body })))
}

export function useSetupBusiness() {
  return useSessionMutation((body: Schemas['SetupBusinessRequest']) =>
    unwrap(api.POST('/api/auth/setup-business', { body })),
  )
}

export function useLogout() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: () => unwrap(api.POST('/api/auth/logout')),
    onSettled: () => {
      qc.clear()
      qc.setQueryData(sessionKey, null)
    },
  })
}
