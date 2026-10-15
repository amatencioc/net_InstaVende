import { createFileRoute, Outlet } from '@tanstack/react-router'
import { PageContainer, PageHeader } from '@/components/shared/page-header'
import { SettingsNav } from '@/features/settings/components/settings-nav'

export const Route = createFileRoute('/_app/settings')({
  head: () => ({ meta: [{ title: 'Configuración — InstaVende' }] }),
  component: SettingsLayout,
})

/** Configuración de la cuenta y del negocio: navegación lateral + una ruta por sección. */
function SettingsLayout() {
  return (
    <PageContainer className="max-w-6xl">
      <PageHeader title="Configuración" description="Administra tu cuenta, tu negocio y tu equipo." />
      <div className="grid gap-6 lg:grid-cols-[15rem_minmax(0,1fr)] lg:gap-8">
        <aside className="lg:sticky lg:top-20 lg:self-start">
          <SettingsNav />
        </aside>
        <div className="min-w-0">
          <Outlet />
        </div>
      </div>
    </PageContainer>
  )
}
