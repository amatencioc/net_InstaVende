import { createFileRoute } from '@tanstack/react-router'
import { knowledgeListQuery } from '@/features/seller-data/api'
import { KnowledgePage } from '@/features/seller-data/components/knowledge/knowledge-page'

export const Route = createFileRoute('/_app/seller/knowledge')({
  head: () => ({ meta: [{ title: 'Base de conocimiento — InstaVende' }] }),
  // Precarga sin bloquear la navegación: la página muestra su propio esqueleto y errores
  loader: ({ context }) => void context.queryClient.prefetchQuery(knowledgeListQuery({})),
  component: KnowledgePage,
})
