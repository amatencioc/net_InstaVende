import { createFileRoute } from '@tanstack/react-router'
import { BusinessSettings } from '@/features/settings/components/business-settings'
import { AdminOnlyNotice } from '@/features/settings/components/settings-card'
import { useIsAdmin } from '@/lib/auth/session'

export const Route = createFileRoute('/_app/settings/business')({
  head: () => ({ meta: [{ title: 'Negocio — Configuración — InstaVende' }] }),
  component: BusinessPage,
})

function BusinessPage() {
  return useIsAdmin() ? <BusinessSettings /> : <AdminOnlyNotice section="Negocio" />
}
