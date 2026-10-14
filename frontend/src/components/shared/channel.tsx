import type { ComponentType, SVGProps } from 'react'
import { InstagramIcon, MessengerIcon, WhatsAppIcon } from './brand-icons'
import type { ChannelType } from '@/lib/realtime/inbox-hub'
import { cn } from '@/lib/utils'

export const channelMeta: Record<ChannelType, { label: string; className: string; icon: ComponentType<SVGProps<SVGSVGElement>> }> = {
  WhatsApp: { label: 'WhatsApp', className: 'text-whatsapp bg-whatsapp/10', icon: WhatsAppIcon },
  Instagram: { label: 'Instagram', className: 'text-instagram bg-instagram/10', icon: InstagramIcon },
  FacebookMessenger: { label: 'Messenger', className: 'text-messenger bg-messenger/10', icon: MessengerIcon },
}

/** Icono redondo con el color del canal. */
export function ChannelIcon({ channel, className }: { channel: ChannelType; className?: string }) {
  const meta = channelMeta[channel]
  const Icon = meta.icon
  return (
    <span
      className={cn('inline-flex size-6 items-center justify-center rounded-full', meta.className, className)}
      title={meta.label}
    >
      <Icon className="size-3.5" aria-hidden />
      <span className="sr-only">{meta.label}</span>
    </span>
  )
}

export function ChannelBadge({ channel, className }: { channel: ChannelType; className?: string }) {
  const meta = channelMeta[channel]
  const Icon = meta.icon
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium',
        meta.className,
        className,
      )}
    >
      <Icon className="size-3" aria-hidden />
      {meta.label}
    </span>
  )
}
