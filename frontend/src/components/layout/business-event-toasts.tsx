import { useQueryClient } from '@tanstack/react-query'
import { useRouter } from '@tanstack/react-router'
import { toast } from 'sonner'
import { useInboxEvent } from '@/lib/realtime/inbox-hub'

/** Muestra en cualquier pantalla del panel los avisos de negocio en tiempo real. */
export function BusinessEventToasts() {
  const router = useRouter()
  const queryClient = useQueryClient()

  useInboxEvent('businessEvent', (event) => {
    // Refresca los datos afectados aunque el usuario esté en otra pantalla
    void queryClient.invalidateQueries({ queryKey: [event.type === 'OrderCreated' ? 'orders' : 'inbox'] })
    void queryClient.invalidateQueries({ queryKey: ['dashboard'] })

    const show = event.type === 'OrderCreated' ? toast.success : toast.warning
    show(event.title, {
      description: event.message,
      duration: 10_000,
      action: event.link
        ? {
            label: event.type === 'OrderCreated' ? 'Ver pedido' : 'Abrir chat',
            onClick: () => void router.history.push(event.link!),
          }
        : undefined,
    })
  })

  return null
}
