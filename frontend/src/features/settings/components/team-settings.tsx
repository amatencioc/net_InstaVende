import { useQuery } from '@tanstack/react-query'
import { Clock, Crown, Link2, Mail, MoreHorizontal, ShieldCheck, Trash2, User, UserPlus, Users, X } from 'lucide-react'
import { useState } from 'react'
import { EmptyState } from '@/components/shared/empty-state'
import { useConfirm } from '@/components/shared/confirm-dialog'
import { QueryError } from '@/components/shared/query-state'
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Progress } from '@/components/ui/progress'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { formatDate, formatRelative, initials } from '@/lib/format'
import { cn } from '@/lib/utils'
import {
  roleLabels,
  teamQuery,
  useCancelInvitation,
  useRegenerateInvitation,
  useRemoveMember,
  useUpdateMemberRole,
  type Team,
  type TeamInvitation,
  type TeamInvitationCreated,
  type TeamMember,
} from '../api'
import { InviteDialog, InviteLink } from './invite-dialog'
import { SettingsCard } from './settings-card'

export function TeamSettings() {
  const team = useQuery(teamQuery)
  const [inviteOpen, setInviteOpen] = useState(false)

  if (team.isPending) return <TeamSkeleton />
  if (team.isError) return <QueryError error={team.error} onRetry={() => void team.refetch()} />

  const data = team.data
  const reserved = data.usedSeats + data.invitations.length
  const seatsLeft = Math.max(0, data.maxUsers - reserved)

  return (
    <div className="space-y-6">
      <SeatsCard team={data} seatsLeft={seatsLeft} onInvite={() => setInviteOpen(true)} />
      <MembersCard team={data} />
      <InvitationsCard team={data} />
      {data.canManage && <InviteDialog open={inviteOpen} onOpenChange={setInviteOpen} seatsLeft={seatsLeft} />}
    </div>
  )
}

function SeatsCard({ team, seatsLeft, onInvite }: { team: Team; seatsLeft: number; onInvite: () => void }) {
  const pending = team.invitations.length
  const percent = Math.min(100, Math.round(((team.usedSeats + pending) / team.maxUsers) * 100))
  const full = seatsLeft === 0

  const inviteButton = (
    <Button onClick={onInvite} disabled={!team.canManage || full}>
      <UserPlus /> Invitar
    </Button>
  )

  return (
    <Card className="gap-0 py-0">
      <CardContent className="flex flex-col gap-4 p-5 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex min-w-0 flex-1 items-center gap-4">
          <span className="bg-primary/10 text-primary flex size-11 shrink-0 items-center justify-center rounded-xl">
            <Users className="size-5" aria-hidden />
          </span>
          <div className="min-w-0 flex-1 space-y-2">
            <p className="text-sm">
              <span className="font-display text-xl font-semibold tabular-nums">{team.usedSeats}</span>
              <span className="text-muted-foreground"> de {team.maxUsers} usuarios</span>
              {pending > 0 && (
                <span className="text-muted-foreground">
                  {' '}
                  · {pending} {pending === 1 ? 'invitación pendiente' : 'invitaciones pendientes'}
                </span>
              )}
            </p>
            <Progress
              value={percent}
              aria-label={`Cupo del plan: ${team.usedSeats + pending} de ${team.maxUsers} usados`}
              className={cn('h-1.5 max-w-sm', full && '[&>[data-slot=progress-indicator]]:bg-warning')}
            />
            <p className="text-muted-foreground text-xs">
              {full
                ? 'Llegaste al límite de tu plan. Quita a alguien o cancela una invitación para liberar cupo.'
                : `Puedes invitar a ${seatsLeft} ${seatsLeft === 1 ? 'persona más' : 'personas más'}.`}
            </p>
          </div>
        </div>
        {team.canManage ? (
          inviteButton
        ) : (
          <Tooltip>
            <TooltipTrigger asChild>
              <span tabIndex={0}>{inviteButton}</span>
            </TooltipTrigger>
            <TooltipContent>Solo un administrador puede invitar.</TooltipContent>
          </Tooltip>
        )}
      </CardContent>
    </Card>
  )
}

function MemberAvatar({ member }: { member: TeamMember }) {
  const name = `${member.firstName} ${member.lastName}`.trim()
  return (
    <Avatar className="size-9">
      {member.avatarUrl && <AvatarImage src={member.avatarUrl} alt="" />}
      <AvatarFallback className="bg-primary/15 text-primary text-xs font-semibold">
        {initials(name || member.email)}
      </AvatarFallback>
    </Avatar>
  )
}

function RoleBadge({ member }: { member: Pick<TeamMember, 'role'> & { isOwner?: boolean } }) {
  if (member.isOwner)
    return (
      <Badge className="bg-brand-accent/10 text-brand-accent">
        <Crown aria-hidden /> Propietario
      </Badge>
    )
  return member.role === 'Admin' ? (
    <Badge className="bg-primary/10 text-primary">
      <ShieldCheck aria-hidden /> {roleLabels.Admin}
    </Badge>
  ) : (
    <Badge variant="secondary">
      <User aria-hidden /> {roleLabels.Member}
    </Badge>
  )
}

function MembersCard({ team }: { team: Team }) {
  return (
    <SettingsCard
      title="Miembros"
      description="Personas con acceso al panel de tu negocio. El propietario siempre es administrador."
    >
      <div className="-mx-6 overflow-x-auto">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead className="pl-6">Usuario</TableHead>
              <TableHead className="hidden md:table-cell">Teléfono</TableHead>
              <TableHead>Rol</TableHead>
              <TableHead className="hidden sm:table-cell">Desde</TableHead>
              {team.canManage && (
                <TableHead className="w-12 pr-6">
                  <span className="sr-only">Acciones</span>
                </TableHead>
              )}
            </TableRow>
          </TableHeader>
          <TableBody>
            {team.members.map((m) => {
              const name = `${m.firstName} ${m.lastName}`.trim()
              return (
                <TableRow key={m.userId}>
                  <TableCell className="pl-6">
                    <div className="flex min-w-0 items-center gap-3">
                      <MemberAvatar member={m} />
                      <div className="min-w-0">
                        <p className="truncate font-medium">
                          {name || m.email}
                          {m.isCurrentUser && <span className="text-muted-foreground font-normal"> (tú)</span>}
                        </p>
                        <p className="text-muted-foreground truncate text-xs">{m.email}</p>
                      </div>
                    </div>
                  </TableCell>
                  <TableCell className="text-muted-foreground hidden md:table-cell">{m.phoneNumber || '—'}</TableCell>
                  <TableCell>
                    <RoleBadge member={m} />
                  </TableCell>
                  <TableCell className="text-muted-foreground hidden sm:table-cell">{formatDate(m.joinedAt)}</TableCell>
                  {team.canManage && (
                    <TableCell className="pr-6 text-right">
                      {!m.isOwner && !m.isCurrentUser && <MemberActions member={m} />}
                    </TableCell>
                  )}
                </TableRow>
              )
            })}
          </TableBody>
        </Table>
      </div>
    </SettingsCard>
  )
}

function MemberActions({ member }: { member: TeamMember }) {
  const confirm = useConfirm()
  const updateRole = useUpdateMemberRole()
  const remove = useRemoveMember()
  const name = `${member.firstName} ${member.lastName}`.trim() || member.email
  const nextRole = member.role === 'Admin' ? 'Member' : 'Admin'

  const onChangeRole = async () => {
    const ok = await confirm({
      title: nextRole === 'Admin' ? `¿Hacer administrador a ${name}?` : `¿Quitar permisos de administrador a ${name}?`,
      description:
        nextRole === 'Admin'
          ? 'Podrá cambiar la configuración del negocio, los canales y el equipo.'
          : 'Seguirá atendiendo mensajes y pedidos, pero no podrá cambiar la configuración.',
      confirmLabel: nextRole === 'Admin' ? 'Hacer administrador' : 'Cambiar a miembro',
    })
    if (ok) updateRole.mutate({ userId: member.userId, role: nextRole })
  }

  const onRemove = async () => {
    const ok = await confirm({
      title: `¿Quitar a ${name} del equipo?`,
      description: 'Perderá el acceso al panel de inmediato. Su cuenta no se elimina y podrás invitarla de nuevo.',
      confirmLabel: 'Quitar del equipo',
      tone: 'destructive',
    })
    if (ok) remove.mutate(member.userId)
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" size="icon-sm" aria-label={`Acciones para ${name}`} disabled={updateRole.isPending || remove.isPending}>
          <MoreHorizontal />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-56">
        <DropdownMenuLabel className="text-muted-foreground truncate text-xs font-normal">{member.email}</DropdownMenuLabel>
        <DropdownMenuItem onSelect={() => void onChangeRole()}>
          {nextRole === 'Admin' ? <ShieldCheck /> : <User />}
          {nextRole === 'Admin' ? 'Hacer administrador' : 'Cambiar a miembro'}
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem variant="destructive" onSelect={() => void onRemove()}>
          <Trash2 /> Quitar del equipo
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

function InvitationsCard({ team }: { team: Team }) {
  const [regenerated, setRegenerated] = useState<TeamInvitationCreated | null>(null)

  return (
    <SettingsCard
      title="Invitaciones pendientes"
      description="Cada invitación reserva un cupo hasta que se acepta, se cancela o vence."
    >
      {team.invitations.length === 0 ? (
        <EmptyState
          icon={Mail}
          title="No hay invitaciones pendientes"
          description={team.canManage ? 'Invita a tu equipo con el botón «Invitar».' : undefined}
          className="py-8"
        />
      ) : (
        <ul className="divide-y rounded-lg border">
          {team.invitations.map((inv) => (
            <InvitationRow key={inv.id} invitation={inv} canManage={team.canManage} onRegenerated={setRegenerated} />
          ))}
        </ul>
      )}
      <Dialog open={regenerated !== null} onOpenChange={(open) => !open && setRegenerated(null)}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>Nuevo enlace de invitación</DialogTitle>
            <DialogDescription>El enlace anterior dejó de funcionar. Comparte este con {regenerated?.invitation.email}.</DialogDescription>
          </DialogHeader>
          {regenerated && <InviteLink created={regenerated} />}
          <DialogFooter>
            <Button onClick={() => setRegenerated(null)}>Listo</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </SettingsCard>
  )
}

function InvitationRow({
  invitation,
  canManage,
  onRegenerated,
}: {
  invitation: TeamInvitation
  canManage: boolean
  onRegenerated: (created: TeamInvitationCreated) => void
}) {
  const confirm = useConfirm()
  const regenerate = useRegenerateInvitation()
  const cancel = useCancelInvitation()

  const onRegenerate = async () => {
    const ok = await confirm({
      title: '¿Generar un enlace nuevo?',
      description:
        'Por seguridad no guardamos el enlace original, así que crearemos uno nuevo válido por 7 días. El enlace anterior dejará de funcionar.',
      confirmLabel: 'Generar enlace',
    })
    if (ok) regenerate.mutate(invitation.id, { onSuccess: onRegenerated })
  }

  const onCancel = async () => {
    const ok = await confirm({
      title: `¿Cancelar la invitación a ${invitation.email}?`,
      description: 'El enlace dejará de funcionar y se liberará el cupo.',
      confirmLabel: 'Cancelar invitación',
      cancelLabel: 'Volver',
      tone: 'destructive',
    })
    if (ok) cancel.mutate(invitation.id)
  }

  return (
    <li className="flex flex-col gap-3 p-3 sm:flex-row sm:items-center sm:justify-between">
      <div className="flex min-w-0 items-center gap-3">
        <span className="bg-muted text-muted-foreground flex size-9 shrink-0 items-center justify-center rounded-full">
          <Mail className="size-4" aria-hidden />
        </span>
        <div className="min-w-0">
          <p className="truncate text-sm font-medium">{invitation.email}</p>
          <p className="text-muted-foreground flex flex-wrap items-center gap-x-2 text-xs">
            <span>{roleLabels[invitation.role]}</span>
            <span aria-hidden>·</span>
            <span className="inline-flex items-center gap-1">
              <Clock className="size-3" aria-hidden /> vence {formatRelative(invitation.expiresAt)}
            </span>
          </p>
        </div>
      </div>
      {canManage && (
        <div className="flex shrink-0 gap-2">
          <Button variant="outline" size="sm" onClick={() => void onRegenerate()} disabled={regenerate.isPending}>
            <Link2 /> Copiar nuevo enlace
          </Button>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => void onCancel()}
            disabled={cancel.isPending}
            aria-label={`Cancelar invitación a ${invitation.email}`}
          >
            <X /> <span className="sm:sr-only">Cancelar</span>
          </Button>
        </div>
      )}
    </li>
  )
}

function TeamSkeleton() {
  return (
    <div className="space-y-6" aria-busy="true" aria-label="Cargando equipo">
      <Skeleton className="h-24 w-full rounded-xl" />
      <Card>
        <CardContent className="space-y-4">
          <Skeleton className="h-5 w-32" />
          {[0, 1, 2].map((i) => (
            <div key={i} className="flex items-center gap-3">
              <Skeleton className="size-9 rounded-full" />
              <div className="flex-1 space-y-1.5">
                <Skeleton className="h-4 w-40" />
                <Skeleton className="h-3 w-56 max-w-full" />
              </div>
              <Skeleton className="h-5 w-20 rounded-full" />
            </div>
          ))}
        </CardContent>
      </Card>
      <Skeleton className="h-32 w-full rounded-xl" />
    </div>
  )
}
