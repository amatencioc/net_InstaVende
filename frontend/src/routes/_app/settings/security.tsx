import { createFileRoute } from '@tanstack/react-router'
import { SecuritySettings } from '@/features/settings/components/security-settings'

export const Route = createFileRoute('/_app/settings/security')({
  head: () => ({ meta: [{ title: 'Seguridad — Configuración — InstaVende' }] }),
  component: SecuritySettings,
})
