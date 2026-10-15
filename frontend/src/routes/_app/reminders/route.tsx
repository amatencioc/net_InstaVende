import { createFileRoute, Link, Outlet } from '@tanstack/react-router'
import { CalendarClock, Layers } from 'lucide-react'
import { PageContainer, PageHeader } from '@/components/shared/page-header'

export const Route = createFileRoute('/_app/reminders')({
  head: () => ({ meta: [{ title: 'Recordatorios — InstaVende' }] }),
  component: RemindersLayout,
})

const tabs = [
  { to: '/reminders', label: 'Programados', icon: CalendarClock, exact: true },
  { to: '/reminders/templates', label: 'Automáticos por segmento', icon: Layers, exact: false },
] as const

/** Recordatorios: programados manualmente y automáticos por segmento (una ruta por pestaña). */
function RemindersLayout() {
  return (
    <PageContainer>
      <PageHeader
        title="Recordatorios"
        description="Retoma conversaciones y recupera ventas con mensajes de seguimiento en el momento justo."
      />
      <nav aria-label="Secciones de recordatorios" className="-mx-1 overflow-x-auto px-1">
        <ul className="bg-muted inline-flex min-w-max gap-1 rounded-xl p-1">
          {tabs.map((tab) => (
            <li key={tab.to}>
              <Link
                to={tab.to}
                activeOptions={{ exact: tab.exact, includeSearch: false }}
                className="text-muted-foreground hover:text-foreground flex items-center gap-2 rounded-lg px-3 py-2 text-sm font-medium transition-colors"
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
