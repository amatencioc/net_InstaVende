import { createFileRoute, notFound } from '@tanstack/react-router'
import { ConversationView } from '@/features/inbox/components/conversation-view'

export const Route = createFileRoute('/_app/inbox/$conversationId')({
  params: {
    parse: ({ conversationId }) => {
      const id = Number(conversationId)
      if (!Number.isInteger(id) || id <= 0) throw notFound()
      return { conversationId: id }
    },
    stringify: ({ conversationId }) => ({ conversationId: String(conversationId) }),
  },
  component: ConversationRoute,
})

function ConversationRoute() {
  const { conversationId } = Route.useParams()
  // key: cada conversación monta su propio estado (scroll, borrador, paneles)
  return <ConversationView key={conversationId} conversationId={conversationId} />
}
