import { createFileRoute } from '@tanstack/react-router'
import { Hammer } from 'lucide-react'
import { EmptyState } from '@/components/shared/empty-state'

export const Route = createFileRoute('/_app/seller/rules')({
  component: () => <EmptyState icon={Hammer} title="Reglas y políticas" description="Sección en construcción." />,
})
