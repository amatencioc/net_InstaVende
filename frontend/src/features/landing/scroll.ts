/** Identificadores de las secciones navegables por ancla de la landing. */
export type SectionId = 'como-funciona' | 'funciones' | 'beneficios' | 'precios' | 'faq'

export const prefersReducedMotion = (): boolean =>
  typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches

/**
 * Desplaza suavemente hasta la sección y le pasa el foco (para lectores de pantalla y teclado),
 * respetando `prefers-reduced-motion`. No toca la URL para no interferir con el router.
 */
export function scrollToSection(id: SectionId | 'inicio'): void {
  const el = document.getElementById(id)
  if (!el) return
  el.scrollIntoView({ behavior: prefersReducedMotion() ? 'auto' : 'smooth', block: 'start' })
  el.focus({ preventScroll: true })
}
