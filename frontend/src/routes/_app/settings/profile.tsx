import { createFileRoute } from '@tanstack/react-router'
import { ProfileSettings } from '@/features/settings/components/profile-settings'

export const Route = createFileRoute('/_app/settings/profile')({
  head: () => ({ meta: [{ title: 'Perfil — Configuración — InstaVende' }] }),
  component: ProfileSettings,
})
