import type { ChannelType, ConversationStatus } from '../api'
import { statusMeta } from '../meta'
import { channelMeta } from '@/components/shared/channel'
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar'
import { initials } from '@/lib/format'
import { cn } from '@/lib/utils'

/** Avatar del contacto con iniciales y el icono del canal superpuesto. */
export function ContactAvatar({
  name,
  avatarUrl,
  channel,
  size = 'default',
  className,
}: {
  name: string
  avatarUrl?: string | null
  channel?: ChannelType
  size?: 'default' | 'lg' | 'xl'
  className?: string
}) {
  const meta = channel ? channelMeta[channel] : null
  const Icon = meta?.icon
  return (
    <div className={cn('relative shrink-0', className)}>
      <Avatar
        className={cn(
          size === 'default' && 'size-10',
          size === 'lg' && 'size-11',
          size === 'xl' && 'size-16',
        )}
      >
        {avatarUrl && <AvatarImage src={avatarUrl} alt="" />}
        <AvatarFallback
          className={cn('bg-primary/10 text-primary font-semibold', size === 'xl' ? 'text-lg' : 'text-sm')}
        >
          {initials(name)}
        </AvatarFallback>
      </Avatar>
      {meta && Icon && (
        <span
          className={cn(
            'ring-background bg-background absolute -right-0.5 -bottom-0.5 flex items-center justify-center rounded-full ring-2',
            size === 'xl' ? 'size-6' : 'size-[18px]',
          )}
          title={meta.label}
        >
          <span className={cn('flex size-full items-center justify-center rounded-full', meta.className)}>
            <Icon className={size === 'xl' ? 'size-3.5' : 'size-2.5'} aria-hidden />
          </span>
          <span className="sr-only">{meta.label}</span>
        </span>
      )}
    </div>
  )
}

export function StatusBadge({ status, className }: { status: ConversationStatus; className?: string }) {
  const meta = statusMeta[status]
  const Icon = meta.icon
  return (
    <span
      className={cn(
        'inline-flex shrink-0 items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium whitespace-nowrap',
        meta.className,
        className,
      )}
    >
      <Icon className="size-3" aria-hidden />
      {meta.label}
    </span>
  )
}

/** Punto de color de una etiqueta (el color lo elige el usuario). */
export function LabelDot({ color, className }: { color: string; className?: string }) {
  return (
    <span
      aria-hidden
      className={cn('inline-block size-2 shrink-0 rounded-full', className)}
      style={{ backgroundColor: color }}
    />
  )
}

export function CountBadge({
  value,
  active,
  className,
}: {
  value: number | undefined
  active?: boolean
  className?: string
}) {
  if (value === undefined) return null
  return (
    <span
      className={cn(
        'ml-auto min-w-6 rounded-full px-1.5 py-0.5 text-center text-xs font-medium tabular-nums',
        active ? 'bg-primary text-primary-foreground' : 'text-muted-foreground',
        className,
      )}
    >
      {value > 999 ? '999+' : value}
    </span>
  )
}
