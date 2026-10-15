import { createFileRoute } from '@tanstack/react-router'
import { NotificationSettings } from '@/features/settings/components/notification-settings'
import { AdminOnlyNotice } from '@/features/settings/components/settings-card'
import { useIsAdmin } from '@/lib/auth/session'

export const Route = createFileRoute('/_app/settings/notifications')({
  head: () => ({ meta: [{ title: 'Notificaciones — Configuración — InstaVende' }] }),
  component: NotificationsPage,
})

function NotificationsPage() {
  return useIsAdmin() ? <NotificationSettings /> : <AdminOnlyNotice section="Notificaciones" />
}
