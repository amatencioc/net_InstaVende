import { useQuery } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { ArrowRight, Mail } from 'lucide-react'
import { Logo } from '@/components/layout/app-sidebar'
import { Accordion, AccordionContent, AccordionItem, AccordionTrigger } from '@/components/ui/accordion'
import { Button } from '@/components/ui/button'
import { sessionQuery } from '@/lib/auth/session'
import { CONTACT_EMAIL, faqs, navLinks } from '../data'
import { AnchorLink, Reveal, Section, SectionHeading } from './primitives'

export function Faq() {
  return (
    <Section id="faq" labelledBy="faq-title" className="bg-muted/30">
      <div className="grid gap-10 lg:grid-cols-[1fr_1.6fr] lg:gap-16">
        <SectionHeading
          id="faq-title"
          eyebrow="Preguntas frecuentes"
          title="Resolvemos tus dudas"
          description={
            <>
              ¿No encuentras lo que buscas? Escríbenos a{' '}
              <a
                href={`mailto:${CONTACT_EMAIL}`}
                className="text-primary font-medium underline-offset-4 hover:underline"
              >
                {CONTACT_EMAIL}
              </a>
              .
            </>
          }
          className="mb-0 text-center lg:sticky lg:top-24 lg:mb-0 lg:self-start lg:text-left"
        />
        <Reveal delay={100}>
          <Accordion type="single" collapsible className="bg-card rounded-2xl border px-5 shadow-xs sm:px-6">
            {faqs.map((f, i) => (
              <AccordionItem key={f.q} value={`faq-${i}`}>
                <AccordionTrigger className="py-5 text-base hover:no-underline">{f.q}</AccordionTrigger>
                <AccordionContent className="text-muted-foreground pb-5 text-[15px] leading-relaxed">
                  {f.a}
                </AccordionContent>
              </AccordionItem>
            ))}
          </Accordion>
        </Reveal>
      </div>
    </Section>
  )
}

export function FinalCta() {
  const { data: session } = useQuery(sessionQuery)
  return (
    <section aria-labelledby="cta-title" className="px-4 py-20 sm:px-6 md:py-28">
      <Reveal className="mx-auto max-w-6xl">
        <div className="bg-primary text-primary-foreground relative isolate overflow-hidden rounded-3xl px-6 py-16 text-center shadow-2xl sm:px-12 md:py-20">
          <div aria-hidden className="pointer-events-none absolute inset-0 -z-10">
            <div className="bg-brand-accent/50 absolute -top-32 -right-24 size-96 rounded-full blur-3xl" />
            <div className="bg-primary-foreground/10 absolute -bottom-40 -left-20 size-96 rounded-full blur-3xl" />
            <div className="absolute inset-0 [background-image:radial-gradient(circle_at_1px_1px,var(--primary-foreground)_1px,transparent_0)] [background-size:22px_22px] opacity-20" />
          </div>
          <h2
            id="cta-title"
            className="mx-auto max-w-3xl text-3xl font-semibold text-balance sm:text-4xl md:text-5xl"
          >
            Tu próximo cliente ya te está escribiendo
          </h2>
          <p className="mx-auto mt-5 max-w-xl text-lg opacity-90">
            Crea tu vendedor IA hoy y deja que responda, negocie y registre pedidos mientras tú haces crecer
            tu negocio.
          </p>
          <div className="mt-9 flex flex-col justify-center gap-3 sm:flex-row">
            {session ? (
              <Button asChild size="lg" variant="secondary" className="h-12 px-7 text-base">
                <Link to="/dashboard">
                  Ir al panel <ArrowRight aria-hidden />
                </Link>
              </Button>
            ) : (
              <>
                <Button asChild size="lg" variant="secondary" className="h-12 px-7 text-base shadow-lg">
                  <Link to="/register">
                    Crear mi vendedor gratis <ArrowRight aria-hidden />
                  </Link>
                </Button>
                <Button
                  asChild
                  size="lg"
                  variant="ghost"
                  className="hover:bg-primary-foreground/10 dark:hover:bg-primary-foreground/10 hover:text-primary-foreground border-primary-foreground/30 h-12 border px-7 text-base"
                >
                  <Link to="/login">Ya tengo cuenta</Link>
                </Button>
              </>
            )}
          </div>
          <p className="mt-6 text-sm opacity-80">Sin tarjeta de crédito · Configúralo en minutos</p>
        </div>
      </Reveal>
    </section>
  )
}

const linkClass =
  'text-muted-foreground hover:text-foreground focus-visible:ring-ring/50 rounded-sm text-sm transition-colors outline-none focus-visible:ring-[3px]'

/** Pie de página. En páginas que no son la landing, los enlaces de sección navegan a "/#seccion". */
export function SiteFooter({ onLanding = true }: { onLanding?: boolean }) {
  return (
    <footer className="border-t px-4 pt-16 pb-10 sm:px-6">
      <div className="mx-auto max-w-6xl">
        <div className="grid grid-cols-2 gap-10 lg:grid-cols-[1.5fr_1fr_1fr_1fr]">
          <div className="col-span-2 lg:col-span-1">
            <Logo />
            <p className="text-muted-foreground mt-4 max-w-xs text-sm">
              Tu vendedor con IA para WhatsApp, Instagram y Messenger. Vende más, responde menos.
            </p>
            <a
              href={`mailto:${CONTACT_EMAIL}`}
              className={`${linkClass} mt-4 inline-flex items-center gap-2`}
            >
              <Mail className="size-4" aria-hidden /> {CONTACT_EMAIL}
            </a>
          </div>
          <nav aria-labelledby="footer-product">
            <h2 id="footer-product" className="font-sans text-sm font-semibold tracking-normal">
              Producto
            </h2>
            <ul className="mt-4 space-y-3">
              {navLinks.map((l) => (
                <li key={l.id}>
                  {onLanding ? (
                    <AnchorLink to={l.id} className={linkClass}>
                      {l.label}
                    </AnchorLink>
                  ) : (
                    <Link to="/" hash={l.id} className={linkClass}>
                      {l.label}
                    </Link>
                  )}
                </li>
              ))}
            </ul>
          </nav>
          <nav aria-labelledby="footer-account">
            <h2 id="footer-account" className="font-sans text-sm font-semibold tracking-normal">
              Cuenta
            </h2>
            <ul className="mt-4 space-y-3">
              <li>
                <Link to="/register" className={linkClass}>
                  Crear cuenta gratis
                </Link>
              </li>
              <li>
                <Link to="/login" className={linkClass}>
                  Iniciar sesión
                </Link>
              </li>
            </ul>
          </nav>
          <nav aria-labelledby="footer-legal">
            <h2 id="footer-legal" className="font-sans text-sm font-semibold tracking-normal">
              Legal
            </h2>
            <ul className="mt-4 space-y-3">
              <li>
                <Link to="/privacy" className={linkClass}>
                  Política de privacidad
                </Link>
              </li>
              <li>
                <Link to="/terms" className={linkClass}>
                  Términos y condiciones
                </Link>
              </li>
            </ul>
          </nav>
        </div>
        <div className="text-muted-foreground mt-14 flex flex-col items-center justify-between gap-3 border-t pt-8 text-xs sm:flex-row">
          <p>© {new Date().getFullYear()} InstaVende. Todos los derechos reservados.</p>
          <p>Hecho en Perú para emprendedores de Latinoamérica.</p>
        </div>
      </div>
    </footer>
  )
}
