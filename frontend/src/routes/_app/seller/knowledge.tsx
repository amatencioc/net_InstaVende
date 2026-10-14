import { createFileRoute } from '@tanstack/react-router'
import { Hammer } from 'lucide-react'
import { EmptyState } from '@/components/shared/empty-state'

export const Route = createFileRoute('/_app/seller/knowledge')({
  component: () => <EmptyState icon={Hammer} title="Base de conocimiento" description="Sección en construcción." />,
})
