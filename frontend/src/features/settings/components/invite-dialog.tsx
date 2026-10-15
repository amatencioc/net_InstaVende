import { zodResolver } from '@hookform/resolvers/zod'
import { Info, Loader2, UserPlus } from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Alert } from '@/components/shared/alert'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Form, FormControl, FormDescription, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group'
import { ApiError } from '@/lib/api/client'
import { formatDateTime } from '@/lib/format'
import { roleLabels, useInviteMember, type TeamInvitationCreated } from '../api'
import { inviteSchema, type InviteFormValues } from '../schemas'
import { CopyField } from './copy-field'

const roleHelp = {
  Member: 'Atiende mensajes, pedidos y productos. No cambia la configuración del negocio ni del equipo.',
  Admin: 'Acceso completo: configuración del negocio, equipo, canales y notificaciones.',
} as const

/** Enlace generado: solo se puede ver una vez (el servidor guarda únicamente su hash). */
export function InviteLink({ created }: { created: TeamInvitationCreated }) {
  return (
    <div className="space-y-4">
      <CopyField value={created.inviteUrl} label="Enlace de invitación" />
      <Alert tone="info" title="Compártelo tú mismo">
        InstaVende no envía emails todavía. Envía este enlace a <strong>{created.invitation.email}</strong> por WhatsApp,
        email o como prefieras. Por seguridad no podremos volver a mostrarlo: si lo pierdes, genera uno nuevo desde las
        invitaciones pendientes. Vence el {formatDateTime(created.invitation.expiresAt)}.
      </Alert>
    </div>
  )
}

export function InviteDialog({
  open,
  onOpenChange,
  seatsLeft,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  seatsLeft: number
}) {
  const invite = useInviteMember()
  const [created, setCreated] = useState<TeamInvitationCreated | null>(null)
  const form = useForm<InviteFormValues>({
    resolver: zodResolver(inviteSchema),
    defaultValues: { email: '', role: 'Member' },
  })

  const close = (next: boolean) => {
    onOpenChange(next)
    if (!next) {
      // Se limpia al cerrar para que el enlace no quede visible al reabrir
      setCreated(null)
      form.reset()
      invite.reset()
    }
  }

  const onSubmit = form.handleSubmit((values) =>
    invite.mutate(
      { email: values.email.trim(), role: values.role },
      {
        onSuccess: setCreated,
        onError: (error) => {
          if (error instanceof ApiError)
            for (const [field, message] of Object.entries(error.fieldErrors))
              form.setError(field as keyof InviteFormValues, { message })
        },
      },
    ),
  )

  const hasFieldErrors = invite.error instanceof ApiError && Object.keys(invite.error.fieldErrors).length > 0

  return (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent className="sm:max-w-lg">
        {created ? (
          <>
            <DialogHeader>
              <DialogTitle>Invitación creada</DialogTitle>
              <DialogDescription>
                {created.invitation.email} se unirá como {roleLabels[created.invitation.role].toLowerCase()} al abrir el
                enlace.
              </DialogDescription>
            </DialogHeader>
            <InviteLink created={created} />
            <DialogFooter>
              <Button
                variant="outline"
                onClick={() => {
                  setCreated(null)
                  form.reset()
                  invite.reset()
                }}
              >
                <UserPlus /> Invitar a otra persona
              </Button>
              <Button onClick={() => close(false)}>Listo</Button>
            </DialogFooter>
          </>
        ) : (
          <Form {...form}>
            <form onSubmit={onSubmit} noValidate className="grid gap-4">
              <DialogHeader>
                <DialogTitle>Invitar a tu equipo</DialogTitle>
                <DialogDescription>
                  Generaremos un enlace personal que vence en 7 días. Te quedan {seatsLeft}{' '}
                  {seatsLeft === 1 ? 'cupo' : 'cupos'} en tu plan.
                </DialogDescription>
              </DialogHeader>
              {invite.error && !hasFieldErrors && <Alert tone="destructive">{invite.error.message}</Alert>}
              <FormField
                control={form.control}
                name="email"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Email</FormLabel>
                    <FormControl>
                      <Input type="email" autoComplete="off" placeholder="persona@tunegocio.com" autoFocus {...field} />
                    </FormControl>
                    <FormDescription>Solo podrá aceptar la invitación con una cuenta de este email.</FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="role"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Rol</FormLabel>
                    <FormControl>
                      <RadioGroup value={field.value} onValueChange={field.onChange} className="grid gap-2 sm:grid-cols-2">
                        {(['Member', 'Admin'] as const).map((role) => (
                          <label
                            key={role}
                            className="has-[[data-state=checked]]:border-primary has-[[data-state=checked]]:bg-primary/5 flex cursor-pointer gap-3 rounded-lg border p-3 transition-colors"
                          >
                            <RadioGroupItem value={role} className="mt-0.5" />
                            <span className="space-y-1">
                              <span className="block text-sm font-medium">{roleLabels[role]}</span>
                              <span className="text-muted-foreground block text-xs">{roleHelp[role]}</span>
                            </span>
                          </label>
                        ))}
                      </RadioGroup>
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <p className="text-muted-foreground flex gap-2 text-xs">
                <Info className="mt-0.5 size-3.5 shrink-0" aria-hidden />
                Una cuenta solo puede pertenecer a un negocio. Si la persona ya tiene su propio negocio en InstaVende,
                invítala con otro email.
              </p>
              <DialogFooter>
                <Button type="button" variant="outline" onClick={() => close(false)}>
                  Cancelar
                </Button>
                <Button type="submit" disabled={invite.isPending}>
                  {invite.isPending && <Loader2 className="animate-spin" />}
                  Generar enlace
                </Button>
              </DialogFooter>
            </form>
          </Form>
        )}
      </DialogContent>
    </Dialog>
  )
}
