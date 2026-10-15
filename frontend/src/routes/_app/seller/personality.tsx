import { createFileRoute } from '@tanstack/react-router'
import { sellerProfileQuery } from '@/features/seller-profile/api'
import { PersonalityPage } from '@/features/seller-profile/components/personality-form'

export const Route = createFileRoute('/_app/seller/personality')({
  head: () => ({ meta: [{ title: 'Personalidad · Mi vendedor — InstaVende' }] }),
  loader: ({ context }) => void context.queryClient.prefetchQuery(sellerProfileQuery),
  component: PersonalityPage,
})
