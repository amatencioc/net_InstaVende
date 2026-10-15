import { Link } from '@tanstack/react-router'
import { MessagesSquare, PartyPopper } from 'lucide-react'
import { Button } from '@/components/ui/button'

/** Banner de éxito cuando WhatsApp queda conectado. */
export function ChannelReadyBanner() {
  return (
    <section
      aria-labelledby="channel-ready-title"
      className="border-success/30 from-success/15 via-success/5 relative overflow-hidden rounded-2xl border bg-linear-to-r to-transparent p-5 sm:p-6"
    >
      <div className="relative z-10 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-start gap-4">
          <div className="bg-success/15 text-success flex size-12 shrink-0 items-center justify-center rounded-full">
            <PartyPopper className="size-6" aria-hidden />
          </div>
          <div className="space-y-1">
            <h2 id="channel-ready-title" className="font-display text-lg font-semibold sm:text-xl">
              ¡Perfecto! Tu canal está listo
            </h2>
            <p className="text-muted-foreground text-sm">
              Ahora puedes empezar a recibir y gestionar las conversaciones de tus clientes.
            </p>
          </div>
        </div>
        <Button asChild className="shrink-0">
          <Link to="/inbox">
            <MessagesSquare /> Ir a Mensajes
          </Link>
        </Button>
      </div>
      <MessagesSquare
        className="text-success/10 pointer-events-none absolute -right-6 -bottom-8 hidden size-40 sm:block"
        aria-hidden
      />
    </section>
  )
}
