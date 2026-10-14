import { createFileRoute } from '@tanstack/react-router'
import { UnderConstruction } from '@/components/shared/under-construction'

export const Route = createFileRoute('/_app/reminders')({
  head: () => ({ meta: [{ title: 'Recordatorios — InstaVende' }] }),
  component: () => <UnderConstruction title="Recordatorios" description="Seguimiento automático por segmento." />,
})
