import { createFileRoute } from '@tanstack/react-router'
import { UnderConstruction } from '@/components/shared/under-construction'

export const Route = createFileRoute('/_app/dashboard')({
  head: () => ({ meta: [{ title: 'Inicio — InstaVende' }] }),
  component: () => <UnderConstruction title="Inicio" description="Resumen de ventas y conversaciones." />,
})
