import { createFileRoute } from '@tanstack/react-router'
import { UnderConstruction } from '@/components/shared/under-construction'

export const Route = createFileRoute('/_app/products')({
  head: () => ({ meta: [{ title: 'Productos — InstaVende' }] }),
  component: () => <UnderConstruction title="Productos" description="Tu catálogo." />,
})
