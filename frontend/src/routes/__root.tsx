import type { QueryClient } from '@tanstack/react-query'
import { createRootRouteWithContext, HeadContent, Link, Outlet, type ErrorComponentProps } from '@tanstack/react-router'
import { lazy, Suspense } from 'react'
import { Toaster } from 'sonner'
import { ConfirmProvider } from '@/components/shared/confirm-dialog'
import { Button } from '@/components/ui/button'
import { TooltipProvider } from '@/components/ui/tooltip'
import { ThemeProvider, useTheme } from '@/lib/theme'

export interface RouterContext {
  queryClient: QueryClient
}

const Devtools = import.meta.env.DEV
  ? lazy(async () => {
      const [{ TanStackRouterDevtools }, { ReactQueryDevtools }] = await Promise.all([
        import('@tanstack/react-router-devtools'),
        import('@tanstack/react-query-devtools'),
      ])
      return {
        default: () => (
          <>
            <TanStackRouterDevtools position="bottom-left" />
            <ReactQueryDevtools buttonPosition="bottom-right" />
          </>
        ),
      }
    })
  : () => null

function ThemedToaster() {
  const { resolved } = useTheme()
  return <Toaster theme={resolved} position="bottom-right" richColors closeButton />
}

export const Route = createRootRouteWithContext<RouterContext>()({
  component: RootComponent,
  notFoundComponent: NotFound,
  errorComponent: RootError,
})

function RootComponent() {
  return (
    <ThemeProvider>
      <HeadContent />
      <TooltipProvider delayDuration={200}>
        <ConfirmProvider>
          <Outlet />
          <ThemedToaster />
          <Suspense>
            <Devtools />
          </Suspense>
        </ConfirmProvider>
      </TooltipProvider>
    </ThemeProvider>
  )
}

function NotFound() {
  return (
    <main className="flex min-h-svh flex-col items-center justify-center gap-4 p-6 text-center">
      <p className="text-primary font-display text-6xl font-bold">404</p>
      <h1 className="text-2xl font-semibold">Página no encontrada</h1>
      <p className="text-muted-foreground">La dirección que buscas no existe o fue movida.</p>
      <Button asChild>
        <Link to="/">Volver al inicio</Link>
      </Button>
    </main>
  )
}

function RootError({ error, reset }: ErrorComponentProps) {
  return (
    <main role="alert" className="flex min-h-svh flex-col items-center justify-center gap-4 p-6 text-center">
      <h1 className="text-2xl font-semibold">Algo salió mal</h1>
      <p className="text-muted-foreground max-w-md">{error instanceof Error ? error.message : 'Error inesperado.'}</p>
      <div className="flex gap-2">
        <Button variant="outline" onClick={reset}>
          Reintentar
        </Button>
        <Button asChild>
          <Link to="/">Ir al inicio</Link>
        </Button>
      </div>
    </main>
  )
}
