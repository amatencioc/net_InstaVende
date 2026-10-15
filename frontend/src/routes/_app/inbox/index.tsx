import { createFileRoute } from '@tanstack/react-router'
import { NoConversationSelected } from '@/features/inbox/components/conversation-view'

export const Route = createFileRoute('/_app/inbox/')({
  component: NoConversationSelected,
})
