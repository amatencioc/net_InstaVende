import { createFileRoute } from '@tanstack/react-router'
import { UnderConstruction } from '@/components/shared/under-construction'

export const Route = createFileRoute('/_app/seller')({
  head: () => ({ meta: [{ title: 'Mi vendedor — InstaVende' }] }),
  component: () => <UnderConstruction title="Mi vendedor" description="Configura tu vendedor IA." />,
})
