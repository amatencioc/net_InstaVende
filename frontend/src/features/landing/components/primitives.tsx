import type { ComponentProps, CSSProperties, ReactNode } from 'react'
import { cn } from '@/lib/utils'
import { useInView } from '../hooks'
import { scrollToSection, type SectionId } from '../scroll'

/** Envuelve contenido que aparece suavemente al entrar en pantalla. */
export function Reveal({
  delay = 0,
  className,
  children,
  ...props
}: ComponentProps<'div'> & { delay?: number }) {
  const [ref, visible] = useInView<HTMLDivElement>()
  return (
    <div
      ref={ref}
      data-visible={visible}
      className={cn('lp-reveal', className)}
      style={{ '--lp-delay': `${delay}ms` } as CSSProperties}
      {...props}
    >
      {children}
    </div>
  )
}

/** Enlace a una sección de la página con scroll suave (mantiene href real para abrir en otra pestaña). */
export function AnchorLink({
  to,
  onNavigate,
  onClick,
  ...props
}: Omit<ComponentProps<'a'>, 'href'> & {
  to: SectionId | 'inicio'
  onNavigate?: (id: SectionId | 'inicio') => void
}) {
  return (
    <a
      href={`#${to}`}
      onClick={(e) => {
        onClick?.(e)
        if (e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return
        e.preventDefault()
        if (onNavigate) onNavigate(to)
        else scrollToSection(to)
      }}
      {...props}
    />
  )
}

/** Sección de la landing con ancla accesible (recibe el foco al navegar por ancla). */
export function Section({
  id,
  className,
  children,
  labelledBy,
}: {
  id?: SectionId
  className?: string
  children: ReactNode
  labelledBy: string
}) {
  return (
    <section
      id={id}
      tabIndex={id ? -1 : undefined}
      aria-labelledby={labelledBy}
      className={cn('scroll-mt-20 px-4 py-20 outline-none sm:px-6 md:py-28', className)}
    >
      <div className="mx-auto max-w-6xl">{children}</div>
    </section>
  )
}

export function SectionHeading({
  id,
  eyebrow,
  title,
  description,
  className,
}: {
  id: string
  eyebrow: string
  title: ReactNode
  description?: ReactNode
  className?: string
}) {
  return (
    <Reveal className={cn('mx-auto mb-12 max-w-2xl text-center md:mb-16', className)}>
      <p className="text-primary mb-3 text-sm font-semibold tracking-wide uppercase">{eyebrow}</p>
      <h2
        id={id}
        className="text-3xl font-semibold text-balance sm:text-4xl md:text-[2.75rem] md:leading-[1.1]"
      >
        {title}
      </h2>
      {description && <p className="text-muted-foreground mt-4 text-lg text-pretty">{description}</p>}
    </Reveal>
  )
}

/** Texto con el degradado de marca (verde → violeta). */
export function GradientText({ children }: { children: ReactNode }) {
  return (
    <span className="from-primary to-brand-accent bg-gradient-to-r bg-clip-text text-transparent">
      {children}
    </span>
  )
}
