import { createFileRoute } from '@tanstack/react-router'
import { quickRepliesQuery, sellerRulesQuery } from '@/features/seller-profile/api'
import { RulesPage } from '@/features/seller-profile/components/rules-form'

export const Route = createFileRoute('/_app/seller/rules')({
  head: () => ({ meta: [{ title: 'Reglas y políticas · Mi vendedor — InstaVende' }] }),
  loader: ({ context }) => {
    void context.queryClient.prefetchQuery(sellerRulesQuery)
    void context.queryClient.prefetchQuery(quickRepliesQuery)
  },
  component: RulesPage,
})
