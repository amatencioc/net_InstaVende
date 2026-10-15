import { createFileRoute } from '@tanstack/react-router'
import { z } from 'zod'
import {
  ScheduledReminders,
  type ScheduledRemindersSearch,
} from '@/features/reminders/components/scheduled-reminders'

const searchSchema = z.object({
  status: z.enum(['Pending', 'Sent', 'Failed', 'Cancelled', 'all']).catch('Pending').default('Pending'),
  q: z.string().catch('').default(''),
  page: z.coerce.number().int().min(1).catch(1).default(1),
})

export const Route = createFileRoute('/_app/reminders/')({
  validateSearch: searchSchema,
  component: ScheduledRemindersPage,
})

function ScheduledRemindersPage() {
  const search = Route.useSearch()
  const navigate = Route.useNavigate()

  const onSearchChange = (next: Partial<ScheduledRemindersSearch>) =>
    void navigate({ search: (prev) => ({ ...prev, ...next }), replace: true })

  return <ScheduledReminders search={search} onSearchChange={onSearchChange} />
}
