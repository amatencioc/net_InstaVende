import { ChannelIcon } from '@/components/shared/channel'
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar'
import type { ChannelType } from '@/lib/realtime/inbox-hub'
import { initials } from '@/lib/format'
import { cn } from '@/lib/utils'

/** Avatar del contacto con el icono del canal superpuesto. */
export function ContactAvatar({
  name,
  avatarUrl,
  channel,
  className,
}: {
  name: string
  avatarUrl?: string | null
  channel: ChannelType
  className?: string
}) {
  return (
    <span className={cn('relative inline-flex shrink-0', className)}>
      <Avatar>
        {avatarUrl && <AvatarImage src={avatarUrl} alt="" />}
        <AvatarFallback className="text-xs font-medium">{initials(name)}</AvatarFallback>
      </Avatar>
      <ChannelIcon
        channel={channel}
        className="ring-background absolute -right-1 -bottom-1 size-4 ring-2 [&_svg]:size-2.5"
      />
    </span>
  )
}
