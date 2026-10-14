import { createFileRoute } from '@tanstack/react-router'
import { Hammer } from 'lucide-react'
import { EmptyState } from '@/components/shared/empty-state'

export const Route = createFileRoute('/_app/seller/personality')({
  component: () => <EmptyState icon={Hammer} title="Personalidad" description="Sección en construcción." />,
})
