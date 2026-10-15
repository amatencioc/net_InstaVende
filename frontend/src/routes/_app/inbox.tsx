import { createFileRoute } from '@tanstack/react-router'
import { InboxLayout } from '@/features/inbox/components/inbox-layout'
import { inboxSearchSchema } from '@/features/inbox/meta'

/** Bandeja de mensajes: filtros en la URL y la conversación abierta como ruta hija. */
export const Route = createFileRoute('/_app/inbox')({
  validateSearch: inboxSearchSchema,
  head: () => ({ meta: [{ title: 'Mensajes — InstaVende' }] }),
  component: InboxLayout,
})
