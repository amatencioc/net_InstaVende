import { zodResolver } from '@hookform/resolvers/zod'
import { useQuery } from '@tanstack/react-query'
import { createFileRoute, Link, useNavigate } from '@tanstack/react-router'
import { Building2, CircleCheck, Clock, Link2Off, Loader2, LogOut, Users, XCircle, type LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { Logo } from '@/components/layout/app-sidebar'
import { Alert } from '@/components/shared/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { invitationQuery, roleLabels, useAcceptInvitation, type InvitationDetails } from '@/features/settings/api'
import { PasswordStrength } from '@/features/settings/components/password-strength'
import { acceptInvitationSchema, type AcceptInvitationFormValues } from '@/features/settings/schemas'
import { ApiError } from '@/lib/api/client'
import { sessionQuery, useLogout, type Session } from '@/lib/auth/session'
import { formatDateTime } from '@/lib/format'

export const Route = createFileRoute('/invite/$token')({
  head: () => ({ meta: [{ title: 'Invitación al equipo — InstaVende' }, { name: 'referrer', content: 'no-referrer' }] }),
  component: InvitePage,
})

function InvitePage() {
  const { token } = Route.useParams()
  const invitation = useQuery(invitationQuery(token))
  const session = useQuery(sessionQuery)

  return (
    <main className="from-primary/10 via-background to-brand-accent/10 flex min-h-svh flex-col items-center justify-center gap-8 bg-gradient-to-br p-4 sm:p-8">
      <Link to="/" aria-label="InstaVende">
        <Logo />
      </Link>
      <Card className="w-full max-w-md gap-0 py-0 shadow-lg">
        <CardContent className="p-6 sm:p-8">
          {invitation.isPending || session.isPending ? (
            <InviteSkeleton />
          ) : invitation.isError ? (
            invitation.error instanceof ApiError && invitation.error.status === 404 ? (
              <StatusMessage
                icon={Link2Off}
                tone="destructive"
                title="Invitación no encontrada"
                text={invitation.error.message}
              />
            ) : (
              <StatusMessage
                icon={XCircle}
                tone="destructive"
                title="No pudimos cargar la invitación"
                text={invitation.error.message}
                action={
                  <Button variant="outline" onClick={() => void invitation.refetch()}>
                    Reintentar
                  </Button>
                }
              />
            )
          ) : (
            <InvitationContent invitation={invitation.data} session={session.data ?? null} token={token} />
          )}
        </CardContent>
      </Card>
      <p className="text-muted-foreground text-center text-xs">
        ¿Recibiste este enlace por error? Puedes ignorarlo, no se creará ninguna cuenta.
      </p>
    </main>
  )
}

const statusCopy = {
  Expired: {
    icon: Clock,
    title: 'Esta invitación expiró',
    text: 'Los enlaces de invitación son válidos durante 7 días. Pide al administrador del negocio que te envíe uno nuevo.',
  },
  Accepted: {
    icon: CircleCheck,
    title: 'Esta invitación ya fue utilizada',
    text: 'Si eres tú quien la aceptó, inicia sesión para entrar al panel del negocio.',
  },
  Cancelled: {
    icon: XCircle,
    title: 'Esta invitación fue cancelada',
    text: 'El administrador del negocio canceló la invitación. Si crees que es un error, pídele un enlace nuevo.',
  },
} as const

function InvitationContent({
  invitation,
  session,
  token,
}: {
  invitation: InvitationDetails
  session: Session | null
  token: string
}) {
  if (invitation.status !== 'Valid') {
    const copy = statusCopy[invitation.status]
    return (
      <div className="space-y-6">
        <BusinessHeader invitation={invitation} />
        <StatusMessage
          icon={copy.icon}
          tone={invitation.status === 'Accepted' ? 'success' : 'warning'}
          title={copy.title}
          text={copy.text}
          action={
            <Button asChild variant="outline">
              <Link to={session ? '/dashboard' : '/login'}>{session ? 'Ir al panel' : 'Iniciar sesión'}</Link>
            </Button>
          }
        />
      </div>
    )
  }

  const invitedEmail = invitation.email ?? ''
  return (
    <div className="space-y-6">
      <BusinessHeader invitation={invitation} />
      {session ? (
        <SignedInAccept invitation={invitation} session={session} token={token} />
      ) : invitation.hasAccount ? (
        <div className="space-y-4">
          <Alert tone="info" title="Ya tienes una cuenta">
            Inicia sesión con <strong>{invitedEmail}</strong> y volverás aquí para unirte al equipo.
          </Alert>
          <Button asChild className="w-full" size="lg">
            <Link to="/login" search={{ redirect: `/invite/${token}` }}>
              Iniciar sesión para unirme
            </Link>
          </Button>
        </div>
      ) : (
        <CreateAccountForm email={invitedEmail} token={token} />
      )}
    </div>
  )
}

function BusinessHeader({ invitation }: { invitation: InvitationDetails }) {
  return (
    <div className="flex flex-col items-center gap-3 text-center">
      <span className="bg-muted flex size-16 items-center justify-center overflow-hidden rounded-2xl border">
        {invitation.businessLogoUrl ? (
          <img src={invitation.businessLogoUrl} alt="" className="size-full object-cover" />
        ) : (
          <Building2 className="text-muted-foreground size-7" aria-hidden />
        )}
      </span>
      <div className="space-y-1">
        <p className="text-muted-foreground text-sm">Te invitaron a unirte al equipo de</p>
        <h1 className="text-2xl font-semibold tracking-tight">{invitation.businessName}</h1>
        {invitation.status === 'Valid' && (
          <p className="text-muted-foreground flex items-center justify-center gap-1.5 text-sm">
            <Users className="size-4" aria-hidden />
            como <span className="text-foreground font-medium">{roleLabels[invitation.role].toLowerCase()}</span>
            {' · '}vence el {formatDateTime(invitation.expiresAt)}
          </p>
        )}
      </div>
    </div>
  )
}

function SignedInAccept({ invitation, session, token }: { invitation: InvitationDetails; session: Session; token: string }) {
  const navigate = useNavigate()
  const accept = useAcceptInvitation(token)
  const logout = useLogout()
  const invitedEmail = invitation.email ?? ''
  const sameEmail = session.user.email.trim().toLowerCase() === invitedEmail.trim().toLowerCase()

  if (!sameEmail)
    return (
      <div className="space-y-4">
        <Alert tone="warning" title="Esta invitación es para otra cuenta">
          Iniciaste sesión como <strong>{session.user.email}</strong>, pero la invitación es para{' '}
          <strong>{invitedEmail}</strong>. Cierra sesión para continuar con ese email.
        </Alert>
        <Button className="w-full" variant="outline" onClick={() => logout.mutate()} disabled={logout.isPending}>
          {logout.isPending ? <Loader2 className="animate-spin" /> : <LogOut />}
          Cerrar sesión y continuar
        </Button>
      </div>
    )

  // Una cuenta solo puede pertenecer a un negocio
  if (session.business)
    return (
      <div className="space-y-4">
        <Alert tone="warning" title="Tu cuenta ya pertenece a otro negocio">
          Tu cuenta <strong>{session.user.email}</strong> ya está vinculada a <strong>{session.business.name}</strong> y
          cada cuenta solo puede pertenecer a un negocio. Pide que te inviten con otro email.
        </Alert>
        <Button asChild className="w-full" variant="outline">
          <Link to="/dashboard">Volver a mi panel</Link>
        </Button>
      </div>
    )

  return (
    <div className="space-y-4">
      {accept.error && <Alert tone="destructive">{accept.error.message}</Alert>}
      <p className="text-muted-foreground text-center text-sm">
        Entrarás con tu cuenta <span className="text-foreground font-medium">{session.user.email}</span>.
      </p>
      <Button
        className="w-full"
        size="lg"
        disabled={accept.isPending}
        onClick={() => accept.mutate({ firstName: null, lastName: null, password: null }, { onSuccess: () => navigate({ to: '/dashboard' }) })}
      >
        {accept.isPending && <Loader2 className="animate-spin" />}
        Unirme a {invitation.businessName}
      </Button>
    </div>
  )
}

function CreateAccountForm({ email, token }: { email: string; token: string }) {
  const navigate = useNavigate()
  const accept = useAcceptInvitation(token)
  const form = useForm<AcceptInvitationFormValues>({
    resolver: zodResolver(acceptInvitationSchema),
    defaultValues: { firstName: '', lastName: '', password: '', confirmPassword: '' },
  })
  const password = useWatch({ control: form.control, name: 'password' })

  const onSubmit = form.handleSubmit(({ firstName, lastName, password }) =>
    accept.mutate(
      { firstName: firstName.trim(), lastName: lastName.trim(), password },
      {
        onSuccess: () => navigate({ to: '/dashboard' }),
        onError: (error) => {
          if (error instanceof ApiError)
            for (const [field, message] of Object.entries(error.fieldErrors))
              if (field === 'firstName' || field === 'lastName' || field === 'password') form.setError(field, { message })
        },
      },
    ),
  )

  const fieldErrors =
    accept.error instanceof ApiError
      ? Object.keys(accept.error.fieldErrors).filter((f) => ['firstName', 'lastName', 'password'].includes(f)).length
      : 0

  return (
    <Form {...form}>
      <form onSubmit={onSubmit} noValidate className="space-y-4">
        <div className="space-y-1 text-center">
          <h2 className="font-semibold">Crea tu cuenta para unirte</h2>
          <p className="text-muted-foreground text-sm">Usarás este email para iniciar sesión.</p>
        </div>
        {accept.error && fieldErrors === 0 && <Alert tone="destructive">{accept.error.message}</Alert>}
        <div className="grid gap-1.5">
          <label htmlFor="invite-email" className="text-sm font-medium">
            Email
          </label>
          <Input id="invite-email" type="email" value={email} autoComplete="username" readOnly disabled />
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          <FormField
            control={form.control}
            name="firstName"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Nombre</FormLabel>
                <FormControl>
                  <Input autoComplete="given-name" autoFocus {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />
          <FormField
            control={form.control}
            name="lastName"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Apellido</FormLabel>
                <FormControl>
                  <Input autoComplete="family-name" {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />
        </div>
        <FormField
          control={form.control}
          name="password"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Contraseña</FormLabel>
              <FormControl>
                <Input type="password" autoComplete="new-password" {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <PasswordStrength password={password} />
        <FormField
          control={form.control}
          name="confirmPassword"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Confirmar contraseña</FormLabel>
              <FormControl>
                <Input type="password" autoComplete="new-password" {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <Button type="submit" className="w-full" size="lg" disabled={accept.isPending}>
          {accept.isPending && <Loader2 className="animate-spin" />}
          Crear cuenta y unirme
        </Button>
        <p className="text-muted-foreground text-center text-sm">
          ¿Ya tienes cuenta con este email?{' '}
          <Link to="/login" search={{ redirect: `/invite/${token}` }} className="text-primary font-medium hover:underline">
            Inicia sesión
          </Link>
        </p>
      </form>
    </Form>
  )
}

function StatusMessage({
  icon: Icon,
  tone,
  title,
  text,
  action,
}: {
  icon: LucideIcon
  tone: 'destructive' | 'warning' | 'success'
  title: string
  text: string
  action?: ReactNode
}) {
  const toneClass = {
    destructive: 'bg-destructive/10 text-destructive',
    warning: 'bg-warning/15 text-warning',
    success: 'bg-success/10 text-success',
  }[tone]
  return (
    <div role={tone === 'destructive' ? 'alert' : 'status'} className="flex flex-col items-center gap-3 text-center">
      <span className={`flex size-12 items-center justify-center rounded-full ${toneClass}`}>
        <Icon className="size-6" aria-hidden />
      </span>
      <h2 className="text-lg font-semibold">{title}</h2>
      <p className="text-muted-foreground text-sm">{text}</p>
      {action && <div className="mt-2">{action}</div>}
    </div>
  )
}

function InviteSkeleton() {
  return (
    <div className="flex flex-col items-center gap-4" aria-busy="true" aria-label="Cargando invitación">
      <Skeleton className="size-16 rounded-2xl" />
      <Skeleton className="h-4 w-48" />
      <Skeleton className="h-7 w-56" />
      <Skeleton className="h-4 w-64" />
      <div className="w-full space-y-3 pt-4">
        <Skeleton className="h-9 w-full" />
        <Skeleton className="h-9 w-full" />
        <Skeleton className="h-10 w-full" />
      </div>
    </div>
  )
}
