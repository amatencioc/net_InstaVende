import { MutationCache, QueryCache, QueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { ApiError, ProblemTypes } from '@/lib/api/client'

/** Redirecciones globales ante sesión expirada o usuario sin negocio. */
function handleAuthErrors(error: unknown): boolean {
  if (!(error instanceof ApiError)) return false
  if (error.status === 401 && !location.pathname.startsWith('/login')) {
    const redirect = encodeURIComponent(location.pathname + location.search)
    location.assign(`/login?redirect=${redirect}`)
    return true
  }
  if (error.status === 403 && error.problem.type === ProblemTypes.noBusiness) {
    location.assign('/setup-business')
    return true
  }
  return false
}

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      gcTime: 5 * 60_000,
      refetchOnWindowFocus: true,
      // No reintentar errores del cliente (4xx): no se van a resolver solos
      retry: (count, error) =>
        !(error instanceof ApiError && error.status >= 400 && error.status < 500) && count < 2,
    },
    mutations: { retry: false },
  },
  queryCache: new QueryCache({ onError: (error) => void handleAuthErrors(error) }),
  mutationCache: new MutationCache({
    onError: (error, _variables, _context, mutation) => {
      if (handleAuthErrors(error) || mutation.meta?.silent) return
      toast.error(error instanceof Error ? error.message : 'Ocurrió un error inesperado.')
    },
    onSuccess: (_data, _variables, _context, mutation) => {
      if (mutation.meta?.successMessage) toast.success(mutation.meta.successMessage)
    },
  }),
})

declare module '@tanstack/react-query' {
  interface Register {
    /** silent: no mostrar el toast de error global. successMessage: toast al completar. */
    mutationMeta: { silent?: boolean; successMessage?: string }
  }
}
