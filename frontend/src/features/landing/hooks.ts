import { useEffect, useRef, useState, useSyncExternalStore } from 'react'

const REDUCED_MOTION = '(prefers-reduced-motion: reduce)'

function subscribeReducedMotion(onChange: () => void) {
  if (typeof matchMedia !== 'function') return () => {}
  const mq = matchMedia(REDUCED_MOTION)
  mq.addEventListener('change', onChange)
  return () => mq.removeEventListener('change', onChange)
}

/** `true` si el usuario pidió reducir el movimiento en su sistema. */
export function usePrefersReducedMotion(): boolean {
  return useSyncExternalStore(
    subscribeReducedMotion,
    () => typeof matchMedia === 'function' && matchMedia(REDUCED_MOTION).matches,
    () => false,
  )
}

interface InViewOptions {
  /** Margen del área de detección (por defecto dispara un poco antes de entrar en pantalla). */
  rootMargin?: string
  threshold?: number
}

/**
 * Detecta cuándo un elemento entra en pantalla con IntersectionObserver.
 * Se dispara una sola vez: después deja de observar para no gastar recursos.
 */
export function useInView<T extends Element>({
  rootMargin = '0px 0px -10% 0px',
  threshold = 0.15,
}: InViewOptions = {}) {
  const ref = useRef<T | null>(null)
  // Sin IntersectionObserver (navegadores muy antiguos) se muestra todo directamente.
  const [inView, setInView] = useState(() => typeof IntersectionObserver === 'undefined')

  useEffect(() => {
    const el = ref.current
    if (!el || inView) return
    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((e) => e.isIntersecting)) {
          setInView(true)
          observer.disconnect()
        }
      },
      { rootMargin, threshold },
    )
    observer.observe(el)
    return () => observer.disconnect()
  }, [inView, rootMargin, threshold])

  return [ref, inView] as const
}

/** `true` cuando la página se ha desplazado más de `offset` píxeles. */
export function useScrolled(offset = 8): boolean {
  const [scrolled, setScrolled] = useState(() => typeof window !== 'undefined' && window.scrollY > offset)
  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > offset)
    window.addEventListener('scroll', onScroll, { passive: true })
    return () => window.removeEventListener('scroll', onScroll)
  }, [offset])
  return scrolled
}
