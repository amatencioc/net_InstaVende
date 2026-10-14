import { createFileRoute } from '@tanstack/react-router'
import { UnderConstruction } from '@/components/shared/under-construction'

export const Route = createFileRoute('/_app/settings')({
  head: () => ({ meta: [{ title: 'Configuración — InstaVende' }] }),
  component: () => <UnderConstruction title="Configuración" description="Tu cuenta y tu negocio." />,
})
