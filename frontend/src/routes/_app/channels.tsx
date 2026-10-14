import { createFileRoute } from '@tanstack/react-router'
import { UnderConstruction } from '@/components/shared/under-construction'

export const Route = createFileRoute('/_app/channels')({
  head: () => ({ meta: [{ title: 'Canales — InstaVende' }] }),
  component: () => <UnderConstruction title="Canales" description="Conecta WhatsApp, Instagram y Messenger." />,
})
