import { useEffect, useState } from 'react'
import { useSession } from '@/lib/auth/session'

/** Devuelve `value` tras `delay` ms sin cambios (búsquedas remotas). */
export function useDebouncedValue<T>(value: T, delay = 300): T {
  const [debounced, setDebounced] = useState(value)
  useEffect(() => {
    const id = window.setTimeout(() => setDebounced(value), delay)
    return () => window.clearTimeout(id)
  }, [value, delay])
  return debounced
}

const browserTimeZone = (() => {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone
  } catch {
    return undefined
  }
})()

/**
 * Zona horaria del negocio si difiere de la del navegador (las fechas del panel
 * se muestran y programan en la hora del dispositivo). null si coinciden.
 */
export function useTimeZoneMismatch(): { business: string; browser: string } | null {
  const { business } = useSession()
  if (!browserTimeZone || !business.timeZone || business.timeZone === browserTimeZone) return null
  // Si el servidor no tiene datos tz devuelve "UTC": no es una diferencia útil de señalar.
  if (business.timeZone === 'UTC' || business.timeZone === 'Etc/UTC') return null
  return { business: business.timeZone, browser: browserTimeZone }
}
