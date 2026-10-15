import { Link } from '@tanstack/react-router'
import { ArrowLeft, TriangleAlert } from 'lucide-react'
import type { ReactNode } from 'react'
import { Logo } from '@/components/layout/app-sidebar'
import { Button } from '@/components/ui/button'
import { CONTACT_EMAIL } from '../data'
import { SiteFooter } from './closing'
import { ThemeToggle } from './site-header'

/** Plantilla de las páginas legales (privacidad, términos). */
export function LegalPage({
  title,
  updatedAt,
  children,
}: {
  title: string
  updatedAt: string
  children: ReactNode
}) {
  return (
    <div className="bg-background text-foreground min-h-svh">
      <header className="bg-background/80 sticky top-0 z-40 border-b backdrop-blur-xl">
        <div className="mx-auto flex h-16 max-w-6xl items-center gap-3 px-4 sm:px-6">
          <Link to="/" aria-label="InstaVende, ir al inicio" className="rounded-lg">
            <Logo />
          </Link>
          <div className="ml-auto flex items-center gap-1">
            <ThemeToggle />
            <Button asChild variant="ghost" size="sm">
              <Link to="/">
                <ArrowLeft aria-hidden /> Volver al inicio
              </Link>
            </Button>
          </div>
        </div>
      </header>

      <main className="px-4 py-12 sm:px-6 md:py-16">
        <article className="mx-auto max-w-3xl">
          <h1 className="text-3xl font-bold sm:text-4xl">{title}</h1>
          <p className="text-muted-foreground mt-2 text-sm">Última actualización: {updatedAt}</p>

          <div
            role="note"
            className="border-warning/50 bg-warning/10 mt-8 flex gap-3 rounded-xl border p-4 text-sm"
          >
            <TriangleAlert className="text-warning mt-0.5 size-5 shrink-0" aria-hidden />
            <p>
              <strong>Borrador pendiente de revisión legal.</strong> Este texto es una plantilla de referencia
              y debe ser revisado y adaptado por un abogado antes de su publicación definitiva. Los datos
              entre corchetes deben completarse.
            </p>
          </div>

          <div className="[&_strong]:text-foreground [&>section]:text-muted-foreground [&>section_h2]:text-foreground mt-10 space-y-8 text-[15px] leading-relaxed [&_h2]:mb-3 [&_h2]:text-xl [&_h2]:font-semibold [&_li]:mt-1.5 [&_p+p]:mt-3 [&_ul]:mt-3 [&_ul]:list-disc [&_ul]:pl-5">
            {children}
          </div>

          <p className="text-muted-foreground mt-12 border-t pt-6 text-sm">
            ¿Tienes preguntas? Escríbenos a{' '}
            <a
              href={`mailto:${CONTACT_EMAIL}`}
              className="text-primary font-medium underline-offset-4 hover:underline"
            >
              {CONTACT_EMAIL}
            </a>
            .
          </p>
        </article>
      </main>

      <SiteFooter onLanding={false} />
    </div>
  )
}
