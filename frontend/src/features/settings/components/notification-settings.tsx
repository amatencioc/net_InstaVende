import { zodResolver } from '@hookform/resolvers/zod'
import { useQuery } from '@tanstack/react-query'
import { BellRing, Hand, Loader2, Mail, Plus, ShoppingBag, Trash2 } from 'lucide-react'
import { useForm } from 'react-hook-form'
import type { z } from 'zod'
import { useConfirm } from '@/components/shared/confirm-dialog'
import { EmptyState } from '@/components/shared/empty-state'
import { QueryError } from '@/components/shared/query-state'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { Switch } from '@/components/ui/switch'
import { ApiError } from '@/lib/api/client'
import {
  notificationEmailsQuery,
  useAddNotificationEmail,
  useDeleteNotificationEmail,
  useToggleNotificationEmail,
  type NotificationEmail,
} from '../api'
import { notificationEmailSchema } from '../schemas'
import { SettingsCard, SettingsCardSkeleton } from './settings-card'

const triggers = [
  { icon: ShoppingBag, title: 'Venta cerrada', text: 'Cuando tu vendedor IA confirma un pedido.' },
  { icon: Hand, title: 'Se necesita a un humano', text: 'Cuando un cliente pide hablar con una persona o el bot no puede resolver.' },
]

export function NotificationSettings() {
  const emails = useQuery(notificationEmailsQuery)

  return (
    <div className="space-y-6">
      <SettingsCard
        title="¿Cuándo te avisamos?"
        description="Recibe avisos cuando tu vendedor IA cierre una venta o cuando sea necesario que un humano tome la conversación."
      >
        <ul className="grid gap-3 sm:grid-cols-2">
          {triggers.map((t) => (
            <li key={t.title} className="flex gap-3 rounded-lg border p-3">
              <span className="bg-primary/10 text-primary flex size-9 shrink-0 items-center justify-center rounded-lg">
                <t.icon className="size-4" aria-hidden />
              </span>
              <span>
                <span className="block text-sm font-medium">{t.title}</span>
                <span className="text-muted-foreground block text-xs">{t.text}</span>
              </span>
            </li>
          ))}
        </ul>
      </SettingsCard>

      {emails.isPending ? (
        <SettingsCardSkeleton rows={2} />
      ) : emails.isError ? (
        <QueryError error={emails.error} onRetry={() => void emails.refetch()} />
      ) : (
        <SettingsCard
          title="Destinatarios"
          description={`Hasta ${emails.data.maxEmails} emails. Pausa uno para dejar de enviarle avisos sin borrarlo.`}
          action={
            <Badge variant="secondary" className="tabular-nums">
              {emails.data.items.length} de {emails.data.maxEmails}
            </Badge>
          }
        >
          <div className="space-y-4">
            <AddEmailForm disabled={emails.data.items.length >= emails.data.maxEmails} />
            {emails.data.items.length === 0 ? (
              <EmptyState
                icon={BellRing}
                title="Aún no hay destinatarios"
                description="Añade al menos un email para enterarte de las ventas y de las conversaciones que necesitan atención."
                className="py-8"
              />
            ) : (
              <ul className="divide-y rounded-lg border">
                {emails.data.items.map((e) => (
                  <EmailRow key={e.id} email={e} />
                ))}
              </ul>
            )}
          </div>
        </SettingsCard>
      )}
    </div>
  )
}

type AddEmailValues = z.infer<typeof notificationEmailSchema>

function AddEmailForm({ disabled }: { disabled: boolean }) {
  const add = useAddNotificationEmail()
  const form = useForm<AddEmailValues>({ resolver: zodResolver(notificationEmailSchema), defaultValues: { email: '' } })

  const onSubmit = form.handleSubmit(({ email }) =>
    add.mutate(email.trim(), {
      onSuccess: () => form.reset(),
      onError: (error) => form.setError('email', { message: error instanceof ApiError ? error.message : 'No se pudo añadir.' }),
    }),
  )

  return (
    <Form {...form}>
      <form onSubmit={onSubmit} noValidate className="flex flex-col gap-2 sm:flex-row sm:items-start">
        <FormField
          control={form.control}
          name="email"
          render={({ field }) => (
            <FormItem className="flex-1">
              <FormLabel className="sr-only">Nuevo email de notificación</FormLabel>
              <FormControl>
                <Input
                  type="email"
                  autoComplete="email"
                  placeholder={disabled ? 'Llegaste al máximo de emails' : 'avisos@tunegocio.com'}
                  disabled={disabled}
                  {...field}
                />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <Button type="submit" disabled={disabled || add.isPending}>
          {add.isPending ? <Loader2 className="animate-spin" /> : <Plus />}
          Añadir email
        </Button>
      </form>
    </Form>
  )
}

function EmailRow({ email }: { email: NotificationEmail }) {
  const confirm = useConfirm()
  const toggle = useToggleNotificationEmail()
  const remove = useDeleteNotificationEmail()
  const switchId = `notification-${email.id}`

  const onDelete = async () => {
    const ok = await confirm({
      title: `¿Eliminar ${email.email}?`,
      description: 'Dejará de recibir los avisos del negocio.',
      confirmLabel: 'Eliminar',
      tone: 'destructive',
    })
    if (ok) remove.mutate(email.id)
  }

  return (
    <li className="flex items-center gap-3 p-3">
      <span className="bg-muted text-muted-foreground flex size-9 shrink-0 items-center justify-center rounded-full">
        <Mail className="size-4" aria-hidden />
      </span>
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm font-medium">{email.email}</p>
        <p className="text-muted-foreground text-xs">{email.isActive ? 'Recibe avisos' : 'En pausa'}</p>
      </div>
      <Switch
        id={switchId}
        checked={email.isActive}
        onCheckedChange={(isActive) => toggle.mutate({ id: email.id, isActive })}
        aria-label={email.isActive ? `Pausar avisos a ${email.email}` : `Activar avisos a ${email.email}`}
      />
      <Button
        variant="ghost"
        size="icon-sm"
        onClick={() => void onDelete()}
        disabled={remove.isPending}
        aria-label={`Eliminar ${email.email}`}
      >
        <Trash2 />
      </Button>
    </li>
  )
}
