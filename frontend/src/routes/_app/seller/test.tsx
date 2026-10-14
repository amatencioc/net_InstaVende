import { createFileRoute } from '@tanstack/react-router'
import { Hammer } from 'lucide-react'
import { EmptyState } from '@/components/shared/empty-state'

export const Route = createFileRoute('/_app/seller/test')({
  component: () => <EmptyState icon={Hammer} title="Probar vendedor" description="Sección en construcción." />,
})
