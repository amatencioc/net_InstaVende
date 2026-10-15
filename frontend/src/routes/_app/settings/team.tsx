import { createFileRoute } from '@tanstack/react-router'
import { TeamSettings } from '@/features/settings/components/team-settings'

export const Route = createFileRoute('/_app/settings/team')({
  head: () => ({ meta: [{ title: 'Equipo — Configuración — InstaVende' }] }),
  component: TeamSettings,
})
