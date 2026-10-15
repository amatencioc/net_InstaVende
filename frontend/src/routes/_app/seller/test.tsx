import { createFileRoute } from '@tanstack/react-router'
import { sellerProfileQuery, sellerRulesQuery } from '@/features/seller-profile/api'
import { TestChatPage } from '@/features/seller-profile/components/test-chat'

export const Route = createFileRoute('/_app/seller/test')({
  head: () => ({ meta: [{ title: 'Probar vendedor · Mi vendedor — InstaVende' }] }),
  loader: ({ context }) => {
    void context.queryClient.prefetchQuery(sellerProfileQuery)
    void context.queryClient.prefetchQuery(sellerRulesQuery)
  },
  component: TestChatPage,
})
