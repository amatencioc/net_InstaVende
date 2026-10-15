import { useCallback, useEffect, useState } from 'react'

/** Devuelve `value` tras `delay` ms sin cambios (búsqueda con debounce). */
export function useDebouncedValue<T>(value: T, delay = 300): T {
  const [debounced, setDebounced] = useState(value)
  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delay)
    return () => clearTimeout(timer)
  }, [value, delay])
  return debounced
}

const NOTIFY_KEY = 'iv.orders.notifyCustomer'

function readNotify(): boolean {
  try {
    return localStorage.getItem(NOTIFY_KEY) === '1'
  } catch {
    return false
  }
}

/** Preferencia del usuario "avisar al cliente al cambiar de estado" (solo en este navegador). */
export function useNotifyPreference(): [boolean, (value: boolean) => void] {
  const [notify, setNotify] = useState(readNotify)
  const update = useCallback((value: boolean) => {
    setNotify(value)
    try {
      localStorage.setItem(NOTIFY_KEY, value ? '1' : '0')
    } catch {
      /* almacenamiento no disponible: se mantiene solo en memoria */
    }
  }, [])
  return [notify, update]
}
