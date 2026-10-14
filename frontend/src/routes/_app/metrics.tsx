import { createFileRoute } from '@tanstack/react-router'
import { UnderConstruction } from '@/components/shared/under-construction'

export const Route = createFileRoute('/_app/metrics')({
  head: () => ({ meta: [{ title: 'Métricas — InstaVende' }] }),
  component: () => <UnderConstruction title="Métricas" description="Rendimiento de ventas y del bot." />,
})
