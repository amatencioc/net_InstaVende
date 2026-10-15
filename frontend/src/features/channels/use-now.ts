import { useEffect, useState } from 'react'

const now = () => Date.now()

/** Marca de tiempo que se actualiza cada `intervalMs` (para cuentas regresivas). */
export function useNow(intervalMs = 500): number {
  const [value, setValue] = useState(now)
  useEffect(() => {
    const id = setInterval(() => setValue(now()), intervalMs)
    return () => clearInterval(id)
  }, [intervalMs])
  return value
}
