import { createFileRoute } from '@tanstack/react-router'
import { paymentImagesQuery, paymentMethodsQuery } from '@/features/seller-data/api'
import { PaymentsPage } from '@/features/seller-data/components/payments/payments-page'

export const Route = createFileRoute('/_app/seller/payments')({
  head: () => ({ meta: [{ title: 'Pagos — InstaVende' }] }),
  // Precarga sin bloquear la navegación: la página muestra su propio esqueleto y errores
  loader: ({ context }) => {
    void context.queryClient.prefetchQuery(paymentMethodsQuery)
    void context.queryClient.prefetchQuery(paymentImagesQuery)
  },
  component: PaymentsPage,
})
