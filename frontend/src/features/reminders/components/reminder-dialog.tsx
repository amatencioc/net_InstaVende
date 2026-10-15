import { zodResolver } from '@hookform/resolvers/zod'
import { addDays, addHours, format, setHours, startOfHour, startOfMinute } from 'date-fns'
import { es } from 'date-fns/locale'
import { CalendarClock, Loader2 } from 'lucide-react'
import { useForm, useWatch } from 'react-hook-form'
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
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { ApiError } from '@/lib/api/client'
import { fromDateTimeLocalInput, toDateTimeLocalInput } from '@/lib/format'
import { cn } from '@/lib/utils'
import { useSaveReminder, type Reminder } from '../api'
import { contactLabel } from '../utils'
import { MAX_SCHEDULE_DAYS, REMINDER_MESSAGE_MAX } from '../constants'
import { reminderFormSchema, type ReminderFormInput, type ReminderFormValues } from '../schemas'
import { ContactCombobox } from './contact-combobox'
import { ChatSurface, MessageBubble } from './message-bubble'

interface ReminderDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  /** Recordatorio a editar; sin valor se crea uno nuevo. */
  reminder?: Reminder | null
  /** Se invoca tras guardar; `created` indica si era un recordatorio nuevo. */
  onSaved?: (created: boolean) => void
}

/** Diálogo para crear o editar un recordatorio programado. */
export function ReminderDialog({ open, onOpenChange, reminder, onSaved }: ReminderDialogProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[calc(100dvh-2rem)] overflow-y-auto sm:max-w-3xl">
        {/* El formulario se monta al abrir: siempre parte de los valores del recordatorio actual. */}
        {open && (
          <ReminderForm
            key={reminder?.id ?? 'new'}
            reminder={reminder ?? null}
            onCancel={() => onOpenChange(false)}
            onSaved={() => {
              onOpenChange(false)
              onSaved?.(!reminder)
            }}
          />
        )}
      </DialogContent>
    </Dialog>
  )
}

const shortcuts: { label: string; compute: () => Date }[] = [
  { label: 'En 1 hora', compute: () => startOfMinute(addHours(new Date(), 1)) },
  { label: 'Mañana 9:00', compute: () => startOfHour(setHours(addDays(new Date(), 1), 9)) },
  { label: 'En 3 días', compute: () => startOfMinute(addDays(new Date(), 3)) },
]

function ReminderForm({
  reminder,
  onCancel,
  onSaved,
}: {
  reminder: Reminder | null
  onCancel: () => void
  onSaved: () => void
}) {
  const save = useSaveReminder()
  const isEdit = reminder !== null

  const form = useForm<ReminderFormInput, unknown, ReminderFormValues>({
    resolver: zodResolver(reminderFormSchema),
    defaultValues: reminder
      ? {
          contact: {
            id: reminder.contactId,
            name: reminder.contactName,
            phone: reminder.contactPhone,
            avatarUrl: reminder.contactAvatarUrl,
            channel: reminder.channel,
          },
          scheduledAt: toDateTimeLocalInput(reminder.scheduledAt),
          message: reminder.message,
        }
      : { contact: null, scheduledAt: '', message: '' },
  })

  const message = useWatch({ control: form.control, name: 'message' }) ?? ''
  const scheduledAt = useWatch({ control: form.control, name: 'scheduledAt' })
  const contact = useWatch({ control: form.control, name: 'contact' })
  const scheduledDate = scheduledAt ? new Date(scheduledAt) : null
  const scheduledValid = scheduledDate !== null && !Number.isNaN(scheduledDate.getTime())

  const onSubmit = form.handleSubmit((values) => {
    save.mutate(
      {
        id: reminder?.id,
        body: {
          contactId: values.contact.id,
          // Al editar sin cambiar de contacto, la API conserva la conversación original.
          conversationId: null,
          message: values.message.trim(),
          scheduledAt: fromDateTimeLocalInput(values.scheduledAt),
          templateKey: null,
        },
      },
      {
        onSuccess: onSaved,
        onError: (error) => {
          if (!(error instanceof ApiError)) return
          for (const [field, msg] of Object.entries(error.fieldErrors)) {
            const name = field === 'contactId' || field === 'conversationId' ? 'contact' : field
            if (name === 'contact' || name === 'scheduledAt' || name === 'message')
              form.setError(name, { message: msg })
          }
        },
      },
    )
  })

  const now = new Date()
  const minValue = toDateTimeLocalInput(now.toISOString())
  const maxValue = toDateTimeLocalInput(addDays(now, MAX_SCHEDULE_DAYS).toISOString())
  const remaining = REMINDER_MESSAGE_MAX - message.length

  return (
    <>
      <DialogHeader>
        <DialogTitle>{isEdit ? 'Editar recordatorio' : 'Nuevo recordatorio'}</DialogTitle>
        <DialogDescription>
          Se enviará automáticamente por el canal del contacto en la fecha y hora indicadas.
        </DialogDescription>
      </DialogHeader>

      <Form {...form}>
        <form onSubmit={onSubmit} noValidate className="grid gap-6 md:grid-cols-[minmax(0,1fr)_260px]">
          <div className="space-y-5">
            {save.error &&
              !(save.error instanceof ApiError && Object.keys(save.error.fieldErrors).length > 0) && (
                <Alert tone="destructive">{save.error.message}</Alert>
              )}

            <FormField
              control={form.control}
              name="contact"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Contacto</FormLabel>
                  <FormControl>
                    <ContactCombobox
                      value={field.value}
                      onChange={(c) => {
                        field.onChange(c)
                        void form.trigger('contact')
                      }}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="scheduledAt"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Fecha y hora de envío</FormLabel>
                  <div className="flex flex-wrap gap-2" role="group" aria-label="Atajos de programación">
                    {shortcuts.map((s) => (
                      <Button
                        key={s.label}
                        type="button"
                        variant="outline"
                        size="sm"
                        className="rounded-full"
                        onClick={() =>
                          form.setValue('scheduledAt', toDateTimeLocalInput(s.compute().toISOString()), {
                            shouldValidate: true,
                            shouldDirty: true,
                          })
                        }
                      >
                        {s.label}
                      </Button>
                    ))}
                  </div>
                  <FormControl>
                    <Input
                      type="datetime-local"
                      min={minValue}
                      max={maxValue}
                      className="w-full sm:w-64"
                      {...field}
                    />
                  </FormControl>
                  <FormDescription>
                    {scheduledValid
                      ? `Se enviará el ${format(scheduledDate, "EEEE d 'de' MMMM 'a las' HH:mm", { locale: es })}.`
                      : `Hasta ${MAX_SCHEDULE_DAYS} días en el futuro.`}
                  </FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="message"
              render={({ field }) => (
                <FormItem>
                  <div className="flex items-center justify-between gap-2">
                    <FormLabel>Mensaje</FormLabel>
                    <span
                      className={cn(
                        'text-xs tabular-nums',
                        remaining < 0
                          ? 'text-destructive'
                          : remaining < 50
                            ? 'text-warning'
                            : 'text-muted-foreground',
                      )}
                      aria-live="polite"
                    >
                      {message.length}/{REMINDER_MESSAGE_MAX}
                    </span>
                  </div>
                  <FormControl>
                    <Textarea
                      rows={5}
                      maxLength={REMINDER_MESSAGE_MAX}
                      placeholder="¡Hola! Te escribo para recordarte que…"
                      className="max-h-64 min-h-28 resize-y"
                      {...field}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          </div>

          <aside aria-label="Vista previa" className="space-y-2">
            <p className="text-muted-foreground text-xs font-medium tracking-wide uppercase">Vista previa</p>
            <ChatSurface className="min-h-48">
              <p className="bg-background/80 text-muted-foreground mx-auto rounded-md px-2 py-0.5 text-[11px] shadow-xs">
                {contact ? contactLabel(contact) : 'Contacto'}
              </p>
              <MessageBubble
                message={message}
                time={scheduledValid ? format(scheduledDate, 'HH:mm') : '--:--'}
              />
            </ChatSurface>
            {scheduledValid && (
              <p className="text-muted-foreground flex items-center gap-1.5 text-xs">
                <CalendarClock className="size-3.5" aria-hidden />
                {format(scheduledDate, 'd MMM yyyy, HH:mm', { locale: es })}
              </p>
            )}
          </aside>

          <DialogFooter className="md:col-span-2">
            <Button type="button" variant="outline" onClick={onCancel} disabled={save.isPending}>
              Cancelar
            </Button>
            <Button type="submit" disabled={save.isPending}>
              {save.isPending && <Loader2 className="animate-spin" aria-hidden />}
              {isEdit ? 'Guardar cambios' : 'Programar recordatorio'}
            </Button>
          </DialogFooter>
        </form>
      </Form>
    </>
  )
}
