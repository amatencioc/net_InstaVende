import { useQuery } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { ArrowRight, Menu, Moon, Sun } from 'lucide-react'
import { useRef, useState } from 'react'
import { Logo } from '@/components/layout/app-sidebar'
import { Button } from '@/components/ui/button'
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from '@/components/ui/sheet'
import { sessionQuery } from '@/lib/auth/session'
import { useTheme } from '@/lib/theme'
import { cn } from '@/lib/utils'
import { navLinks } from '../data'
import { useScrolled } from '../hooks'
import { scrollToSection, type SectionId } from '../scroll'
import { AnchorLink } from './primitives'

export function ThemeToggle({ className }: { className?: string }) {
  const { resolved, setTheme } = useTheme()
  const next = resolved === 'dark' ? 'light' : 'dark'
  return (
    <Button
      variant="ghost"
      size="icon"
      className={className}
      onClick={() => setTheme(next)}
      aria-label={next === 'dark' ? 'Activar modo oscuro' : 'Activar modo claro'}
    >
      {resolved === 'dark' ? <Sun aria-hidden /> : <Moon aria-hidden />}
    </Button>
  )
}

/** Acciones de cuenta: "Ir al panel" con sesión, o iniciar sesión / registro. */
function AccountActions({ stacked = false }: { stacked?: boolean }) {
  // La consulta de sesión devuelve null sin usuario (401) y no muestra errores globales.
  const { data: session, isPending } = useQuery(sessionQuery)

  if (session) {
    return (
      <Button asChild className={cn(stacked && 'w-full')}>
        <Link to="/dashboard">
          Ir al panel <ArrowRight aria-hidden />
        </Link>
      </Button>
    )
  }

  return (
    <div className={cn('flex gap-2', stacked && 'flex-col', isPending && 'opacity-90')}>
      <Button asChild variant="ghost" className={cn(stacked && 'w-full')}>
        <Link to="/login">Iniciar sesión</Link>
      </Button>
      <Button asChild className={cn('shadow-primary/25 shadow-md', stacked && 'w-full')}>
        <Link to="/register">Empezar gratis</Link>
      </Button>
    </div>
  )
}

export function SiteHeader() {
  const scrolled = useScrolled()
  const [open, setOpen] = useState(false)
  const pendingSection = useRef<SectionId | 'inicio' | null>(null)

  return (
    <header
      className={cn(
        'sticky top-0 z-40 border-b transition-[background-color,border-color,box-shadow] duration-300',
        scrolled
          ? 'bg-background/75 border-border shadow-sm backdrop-blur-xl'
          : 'bg-background/40 border-transparent backdrop-blur-md',
      )}
    >
      <div className="mx-auto flex h-16 max-w-6xl items-center gap-4 px-4 sm:px-6">
        <AnchorLink to="inicio" aria-label="InstaVende, ir al inicio" className="rounded-lg">
          <Logo />
        </AnchorLink>

        <nav aria-label="Secciones" className="ml-6 hidden md:block">
          <ul className="flex items-center gap-1">
            {navLinks.map((link) => (
              <li key={link.id}>
                <AnchorLink
                  to={link.id}
                  className="text-muted-foreground hover:text-foreground hover:bg-accent/60 focus-visible:ring-ring/50 rounded-md px-3 py-2 text-sm font-medium transition-colors outline-none focus-visible:ring-[3px]"
                >
                  {link.label}
                </AnchorLink>
              </li>
            ))}
          </ul>
        </nav>

        <div className="ml-auto hidden items-center gap-1 md:flex">
          <ThemeToggle />
          <AccountActions />
        </div>

        <div className="ml-auto flex items-center gap-1 md:hidden">
          <ThemeToggle />
          <Sheet open={open} onOpenChange={setOpen}>
            <SheetTrigger asChild>
              <Button variant="ghost" size="icon" aria-label="Abrir menú">
                <Menu aria-hidden />
              </Button>
            </SheetTrigger>
            <SheetContent
              side="right"
              className="w-[85%] max-w-xs"
              onCloseAutoFocus={(e) => {
                // Si se eligió una sección, se desplaza tras cerrar (cuando ya no hay bloqueo de scroll).
                const target = pendingSection.current
                if (!target) return
                e.preventDefault()
                pendingSection.current = null
                scrollToSection(target)
              }}
            >
              <SheetHeader className="border-b">
                <SheetTitle>
                  <Logo />
                </SheetTitle>
                <SheetDescription className="sr-only">Navegación principal de InstaVende</SheetDescription>
              </SheetHeader>
              <nav aria-label="Secciones" className="px-2">
                <ul className="flex flex-col">
                  {navLinks.map((link) => (
                    <li key={link.id}>
                      <AnchorLink
                        to={link.id}
                        onNavigate={(id) => {
                          pendingSection.current = id
                          setOpen(false)
                        }}
                        className="hover:bg-accent focus-visible:ring-ring/50 flex rounded-md px-3 py-3 text-base font-medium outline-none focus-visible:ring-[3px]"
                      >
                        {link.label}
                      </AnchorLink>
                    </li>
                  ))}
                </ul>
              </nav>
              <div className="mt-auto border-t p-4">
                <AccountActions stacked />
              </div>
            </SheetContent>
          </Sheet>
        </div>
      </div>
    </header>
  )
}
