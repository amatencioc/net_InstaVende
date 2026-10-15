import { zodResolver } from '@hookform/resolvers/zod'
import { Loader2, LogOut, MonitorSmartphone } from 'lucide-react'
import { useForm, useWatch } from 'react-hook-form'
import { Alert } from '@/components/shared/alert'
import { useConfirm } from '@/components/shared/confirm-dialog'
import { Button } from '@/components/ui/button'
import { Form, FormControl, FormDescription, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { ApiError } from '@/lib/api/client'
import { useSession } from '@/lib/auth/session'
import { useChangeEmail, useChangePassword, useRevokeOtherSessions } from '../api'
import {
  changeEmailSchema,
  changePasswordSchema,
  type ChangeEmailFormValues,
  type ChangePasswordFormValues,
} from '../schemas'
import { PasswordStrength } from './password-strength'
import { SettingsCard } from './settings-card'

/** Muestra los errores de validación del servidor junto a cada campo; devuelve true si los había. */
function applyFieldErrors<T extends Record<string, unknown>>(
  error: unknown,
  setError: (name: keyof T & string, error: { message: string }) => void,
): boolean {
  if (!(error instanceof ApiError)) return false
  const entries = Object.entries(error.fieldErrors)
  for (const [field, message] of entries) setError(field as keyof T & string, { message })
  return entries.length > 0
}

export function SecuritySettings() {
  return (
    <div className="space-y-6">
      <PasswordCard />
      <EmailCard />
      <SessionsCard />
    </div>
  )
}

function PasswordCard() {
  const { user } = useSession()
  const change = useChangePassword()
  const form = useForm<ChangePasswordFormValues>({
    resolver: zodResolver(changePasswordSchema),
    defaultValues: { currentPassword: '', newPassword: '', confirmPassword: '' },
  })
  const newPassword = useWatch({ control: form.control, name: 'newPassword' })

  const onSubmit = form.handleSubmit(({ currentPassword, newPassword }) =>
    change.mutate(
      { currentPassword, newPassword },
      {
        onSuccess: () => form.reset(),
        onError: (error) => applyFieldErrors<ChangePasswordFormValues>(error, form.setError),
      },
    ),
  )

  const hasFieldErrors = change.error instanceof ApiError && Object.keys(change.error.fieldErrors).length > 0

  return (
    <Form {...form}>
      <form onSubmit={onSubmit} noValidate>
        <SettingsCard
          title="Contraseña"
          description="Al cambiarla se cerrará tu sesión en los demás dispositivos; aquí seguirás conectado."
          footer={
            <Button type="submit" disabled={change.isPending}>
              {change.isPending && <Loader2 className="animate-spin" />}
              Cambiar contraseña
            </Button>
          }
        >
          <div className="max-w-xl space-y-4">
            {change.error && !hasFieldErrors && <Alert tone="destructive">{change.error.message}</Alert>}
            {/* Campo oculto para que los gestores de contraseñas asocien la cuenta */}
            <input type="text" autoComplete="username" value={user.email} readOnly hidden />
            <FormField
              control={form.control}
              name="currentPassword"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Contraseña actual</FormLabel>
                  <FormControl>
                    <Input type="password" autoComplete="current-password" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="newPassword"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Nueva contraseña</FormLabel>
                  <FormControl>
                    <Input type="password" autoComplete="new-password" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <PasswordStrength password={newPassword} />
            <FormField
              control={form.control}
              name="confirmPassword"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Confirmar nueva contraseña</FormLabel>
                  <FormControl>
                    <Input type="password" autoComplete="new-password" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          </div>
        </SettingsCard>
      </form>
    </Form>
  )
}

function EmailCard() {
  const { user } = useSession()
  const change = useChangeEmail()
  const form = useForm<ChangeEmailFormValues>({
    resolver: zodResolver(changeEmailSchema),
    defaultValues: { newEmail: '', currentPassword: '' },
  })

  const onSubmit = form.handleSubmit((values) =>
    change.mutate(
      { newEmail: values.newEmail.trim(), currentPassword: values.currentPassword },
      {
        onSuccess: () => form.reset(),
        onError: (error) => applyFieldErrors<ChangeEmailFormValues>(error, form.setError),
      },
    ),
  )

  const hasFieldErrors = change.error instanceof ApiError && Object.keys(change.error.fieldErrors).length > 0

  return (
    <Form {...form}>
      <form onSubmit={onSubmit} noValidate>
        <SettingsCard
          title="Email de acceso"
          description={
            <>
              Actualmente inicias sesión con <span className="text-foreground font-medium">{user.email}</span>. El
              nuevo email será tu usuario para entrar a InstaVende.
            </>
          }
          footer={
            <Button type="submit" disabled={change.isPending}>
              {change.isPending && <Loader2 className="animate-spin" />}
              Cambiar email
            </Button>
          }
        >
          <div className="grid max-w-xl gap-4">
            {change.error && !hasFieldErrors && <Alert tone="destructive">{change.error.message}</Alert>}
            <FormField
              control={form.control}
              name="newEmail"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Nuevo email</FormLabel>
                  <FormControl>
                    <Input type="email" autoComplete="email" placeholder="tu@negocio.com" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="currentPassword"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Contraseña actual</FormLabel>
                  <FormControl>
                    <Input type="password" autoComplete="current-password" {...field} />
                  </FormControl>
                  <FormDescription>Por seguridad, confirma que eres tú.</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />
          </div>
        </SettingsCard>
      </form>
    </Form>
  )
}

function SessionsCard() {
  const confirm = useConfirm()
  const revoke = useRevokeOtherSessions()

  const onRevoke = async () => {
    const ok = await confirm({
      title: '¿Cerrar sesión en los demás dispositivos?',
      description:
        'Se cerrará tu sesión en cualquier otro navegador o equipo donde hayas entrado (en los próximos minutos). Aquí seguirás conectado.',
      confirmLabel: 'Cerrar las demás sesiones',
      tone: 'destructive',
    })
    if (ok) revoke.mutate()
  }

  return (
    <SettingsCard
      title="Sesiones activas"
      description="Si usaste InstaVende en un equipo compartido o perdiste un dispositivo, cierra la sesión en todos los demás."
    >
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-center gap-3">
          <span className="bg-primary/10 text-primary flex size-10 shrink-0 items-center justify-center rounded-full">
            <MonitorSmartphone className="size-5" aria-hidden />
          </span>
          <div>
            <p className="text-sm font-medium">Este dispositivo</p>
            <p className="text-muted-foreground text-xs">Sesión actual · se mantiene abierta</p>
          </div>
        </div>
        <Button variant="outline" onClick={onRevoke} disabled={revoke.isPending}>
          {revoke.isPending ? <Loader2 className="animate-spin" /> : <LogOut />}
          Cerrar las demás sesiones
        </Button>
      </div>
    </SettingsCard>
  )
}
