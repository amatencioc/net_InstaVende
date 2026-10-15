import { createFileRoute } from '@tanstack/react-router'
import { deliveryZonesQuery } from '@/features/seller-data/api'
import { DeliveryPage } from '@/features/seller-data/components/delivery/delivery-page'

export const Route = createFileRoute('/_app/seller/delivery')({
  head: () => ({ meta: [{ title: 'Entrega — InstaVende' }] }),
  // Precarga sin bloquear la navegación: la página muestra su propio esqueleto y errores
  loader: ({ context }) => void context.queryClient.prefetchQuery(deliveryZonesQuery),
  component: DeliveryPage,
})
