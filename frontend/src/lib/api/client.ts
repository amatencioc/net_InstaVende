import createClient, { type Middleware } from 'openapi-fetch'
import type { components, paths } from './schema'

export type Schemas = components['schemas']

/** Detalle de error RFC 9457 devuelto por la API. */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  errors?: Record<string, string[]>
}

export const ProblemTypes = {
  noBusiness: 'https://instavende.app/problems/no-business',
  adminRequired: 'https://instavende.app/problems/admin-required',
} as const

/** Error de la API con el mensaje listo para mostrar al usuario. */
export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails

  constructor(status: number, problem: ProblemDetails) {
    super(ApiError.messageFor(status, problem))
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }

  /** Errores de validación por campo (claves en camelCase, como en el formulario). */
  get fieldErrors(): Record<string, string> {
    const out: Record<string, string> = {}
    for (const [key, messages] of Object.entries(this.problem.errors ?? {})) {
      const field = key.charAt(0).toLowerCase() + key.slice(1)
      if (messages[0]) out[field] = messages[0]
    }
    return out
  }

  private static messageFor(status: number, p: ProblemDetails): string {
    if (p.detail) return p.detail
    if (p.errors) {
      const first = Object.values(p.errors).flat()[0]
      if (first) return first
    }
    if (status === 0) return 'No se pudo conectar con el servidor. Revisa tu conexión.'
    if (status === 401) return 'Tu sesión expiró. Vuelve a iniciar sesión.'
    if (status === 403) return 'No tienes permiso para realizar esta acción.'
    if (status === 404) return 'El recurso no existe o fue eliminado.'
    if (status === 429) return 'Demasiadas solicitudes. Espera un momento e inténtalo de nuevo.'
    return p.title ?? 'Ocurrió un error inesperado. Inténtalo de nuevo.'
  }
}

const SAFE_METHODS = new Set(['GET', 'HEAD', 'OPTIONS'])

export function readCookie(name: string): string | null {
  const match = document.cookie.split('; ').find((c) => c.startsWith(`${name}=`))
  return match ? decodeURIComponent(match.slice(name.length + 1)) : null
}

/** Añade el token CSRF (patrón double-submit) a toda petición que modifica datos. */
const csrfMiddleware: Middleware = {
  onRequest({ request }) {
    request.headers.set('X-Requested-With', 'XMLHttpRequest')
    if (!SAFE_METHODS.has(request.method)) {
      const token = readCookie('XSRF-TOKEN')
      if (token) request.headers.set('X-XSRF-TOKEN', token)
    }
    return request
  },
}

export const apiBaseUrl: string = import.meta.env.VITE_API_BASE_URL ?? ''

/** Cliente tipado generado desde el contrato OpenAPI (frontend/openapi/instavende-api.json). */
export const api = createClient<paths>({ baseUrl: apiBaseUrl, credentials: 'include' })
api.use(csrfMiddleware)

/**
 * Convierte la respuesta de openapi-fetch en el dato tipado o lanza {@link ApiError}.
 * Uso: `const products = await unwrap(api.GET('/api/products'))`
 */
export async function unwrap<T>(request: Promise<{ data?: T; error?: unknown; response: Response }>): Promise<T> {
  let result: { data?: T; error?: unknown; response: Response }
  try {
    result = await request
  } catch {
    throw new ApiError(0, {})
  }
  if (result.error !== undefined || !result.response.ok) {
    throw new ApiError(result.response.status, (result.error ?? {}) as ProblemDetails)
  }
  return result.data as T
}

let csrfRequest: Promise<void> | null = null

/** Garantiza que exista la cookie XSRF-TOKEN antes del primer POST (p.ej. login). */
export function ensureCsrf(force = false): Promise<void> {
  if (!force && readCookie('XSRF-TOKEN')) return Promise.resolve()
  csrfRequest ??= fetch(`${apiBaseUrl}/api/auth/csrf`, { credentials: 'include' })
    .then(() => undefined)
    .finally(() => {
      csrfRequest = null
    })
  return csrfRequest
}
