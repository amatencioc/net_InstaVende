import { createFileRoute } from '@tanstack/react-router'
import { UnderConstruction } from '@/components/shared/under-construction'

export const Route = createFileRoute('/_app/inbox')({
  head: () => ({ meta: [{ title: 'Mensajes — InstaVende' }] }),
  component: () => <UnderConstruction title="Mensajes" description="Conversaciones de todos tus canales." />,
})
