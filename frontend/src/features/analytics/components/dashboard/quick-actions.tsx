import { Link } from '@tanstack/react-router'
import type { LucideIcon } from 'lucide-react'
import { BarChart3, FlaskConical, MessagesSquare, PackagePlus } from 'lucide-react'
import type { FileRoutesByTo } from '@/routeTree.gen'

const actions: { to: keyof FileRoutesByTo; title: string; description: string; icon: LucideIcon }[] = [
  { to: '/inbox', title: 'Mensajes', description: 'Responde y supervisa chats', icon: MessagesSquare },
  { to: '/products', title: 'Productos', description: 'Actualiza tu catálogo', icon: PackagePlus },
  {
    to: '/seller/test',
    title: 'Probar vendedor',
    description: 'Simula una conversación',
    icon: FlaskConical,
  },
  { to: '/metrics', title: 'Métricas', description: 'Analiza tus resultados', icon: BarChart3 },
]

/** Accesos rápidos a las tareas más frecuentes. */
export function QuickActions() {
  return (
    <section aria-labelledby="quick-actions-title" className="space-y-3">
      <h2 id="quick-actions-title" className="text-base font-semibold">
        Accesos rápidos
      </h2>
      <ul className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        {actions.map((a) => (
          <li key={a.to}>
            <Link
              to={a.to}
              className="bg-card hover:border-primary/40 hover:bg-primary/5 focus-visible:ring-ring/50 group flex h-full items-center gap-3 rounded-xl border p-4 transition-colors outline-none focus-visible:ring-[3px]"
            >
              <span className="bg-muted text-foreground group-hover:bg-primary/10 group-hover:text-primary flex size-10 shrink-0 items-center justify-center rounded-lg transition-colors">
                <a.icon className="size-5" aria-hidden />
              </span>
              <span className="min-w-0">
                <span className="block text-sm font-medium">{a.title}</span>
                <span className="text-muted-foreground block truncate text-xs">{a.description}</span>
              </span>
            </Link>
          </li>
        ))}
      </ul>
    </section>
  )
}
