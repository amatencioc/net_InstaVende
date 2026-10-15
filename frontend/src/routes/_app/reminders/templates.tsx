import { createFileRoute } from '@tanstack/react-router'
import { SegmentTemplates } from '@/features/reminders/components/segment-templates'

export const Route = createFileRoute('/_app/reminders/templates')({
  head: () => ({ meta: [{ title: 'Recordatorios automáticos — InstaVende' }] }),
  component: SegmentTemplates,
})
