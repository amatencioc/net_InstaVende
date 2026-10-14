import { createFileRoute, Link, Outlet } from '@tanstack/react-router'
import { BookOpen, FlaskConical, ScrollText, Sparkles, Truck, Wallet } from 'lucide-react'
import { PageContainer, PageHeader } from '@/components/shared/page-header'
import { cn } from '@/lib/utils'

export const Route = createFileRoute('/_app/seller')({
  head: () => ({ meta: [{ title: 'Mi vendedor — InstaVende' }] }),
  component: SellerLayout,
})

const tabs = [
  { to: '/seller/personality', label: 'Personalidad', icon: Sparkles },
  { to: '/seller/rules', label: 'Reglas y políticas', icon: ScrollText },
  { to: '/seller/knowledge', label: 'Base de conocimiento', icon: BookOpen },
  { to: '/seller/delivery', label: 'Entrega', icon: Truck },
  { to: '/seller/payments', label: 'Pagos', icon: Wallet },
  { to: '/seller/test', label: 'Probar vendedor', icon: FlaskConical },
] as const

/** Configuración del vendedor IA organizada en pestañas (una ruta por pestaña). */
function SellerLayout() {
  return (
    <PageContainer>
      <PageHeader title="Mi vendedor" description="Configura cómo tu vendedor IA atiende, negocia y vende." />
      <nav aria-label="Secciones del vendedor" className="-mx-1 overflow-x-auto">
        <ul className="bg-muted inline-flex min-w-max gap-1 rounded-xl p-1">
          {tabs.map((tab) => (
            <li key={tab.to}>
              <Link
                to={tab.to}
                className={cn(
                  'text-muted-foreground hover:text-foreground flex items-center gap-2 rounded-lg px-3 py-2 text-sm font-medium transition-colors',
                )}
                activeProps={{ className: 'bg-background text-foreground shadow-sm' }}
              >
                <tab.icon className="size-4" aria-hidden />
                {tab.label}
              </Link>
            </li>
          ))}
        </ul>
      </nav>
      <Outlet />
    </PageContainer>
  )
}
