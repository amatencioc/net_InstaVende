import { createFileRoute } from '@tanstack/react-router'
import { UnderConstruction } from '@/components/shared/under-construction'

export const Route = createFileRoute('/_app/orders')({
  head: () => ({ meta: [{ title: 'Pedidos — InstaVende' }] }),
  component: () => <UnderConstruction title="Pedidos" description="Gestiona tus pedidos." />,
})
