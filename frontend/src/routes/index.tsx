import { createFileRoute, Link } from '@tanstack/react-router'
import { Button } from '@/components/ui/button'
import { Logo } from '@/components/layout/app-sidebar'

export const Route = createFileRoute('/')({
  component: () => (
    <main className="flex min-h-svh flex-col items-center justify-center gap-6 p-6 text-center">
      <Logo />
      <h1 className="text-4xl font-semibold">Tu vendedor con IA, 24/7</h1>
      <div className="flex gap-2">
        <Button asChild>
          <Link to="/register">Crear cuenta</Link>
        </Button>
        <Button asChild variant="outline">
          <Link to="/login">Iniciar sesión</Link>
        </Button>
      </div>
    </main>
  ),
})
