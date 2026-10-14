import { Hammer } from 'lucide-react'
import { EmptyState } from './empty-state'
import { PageContainer, PageHeader } from './page-header'

/** Marcador temporal de un módulo pendiente de implementar en el SPA. */
export function UnderConstruction({ title, description }: { title: string; description?: string }) {
  return (
    <PageContainer>
      <PageHeader title={title} description={description} />
      <EmptyState icon={Hammer} title="Módulo en construcción" description="Esta sección se está migrando al nuevo panel." />
    </PageContainer>
  )
}
