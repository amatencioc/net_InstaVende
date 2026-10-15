import { zodResolver } from '@hookform/resolvers/zod'
import { Loader2 } from 'lucide-react'
import { useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { toast } from 'sonner'
import { useSavePaymentMethod, type PaymentMethod, type PaymentMethodType } from '../../api'
import { PAYMENT_INSTRUCTIONS_MAX, paymentFieldLabels, paymentTypeMeta, paymentTypes } from '../../meta'
import {
  paymentMethodDefaults,
  paymentMethodFormSchema,
  toPaymentMethodRequest,
  type PaymentMethodFormValues,
} from '../../schemas'
import { applyServerErrors } from '../../utils'
import { CharCounter } from '../char-counter'
import { WhatsAppPreview } from './whatsapp-preview'
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
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Switch } from '@/components/ui/switch'
import { Textarea } from '@/components/ui/textarea'
import { useSession } from '@/lib/auth/session'

export interface PaymentMethodDialogState {
  method?: PaymentMethod
}

/** Crear o editar un método de pago con vista previa en vivo del mensaje al cliente. */
export function PaymentMethodDialog({
  state,
  onOpenChange,
}: {
  state: PaymentMethodDialogState | null
  onOpenChange: (open: boolean) => void
}) {
  return (
    <Dialog open={state !== null} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[92svh] overflow-y-auto sm:max-w-4xl">
        {state && <PaymentMethodForm method={state.method} onDone={() => onOpenChange(false)} />}
      </DialogContent>
    </Dialog>
  )
}

const fields = ['type', 'name', 'instructions', 'accountNumber', 'accountAlias', 'paymentLink', 'isActive'] as const

function PaymentMethodForm({ method, onDone }: { method?: PaymentMethod; onDone: () => void }) {
  const { business } = useSession()
  const save = useSavePaymentMethod()
  const [serverError, setServerError] = useState<string | null>(null)
  const form = useForm<PaymentMethodFormValues>({
    resolver: zodResolver(paymentMethodFormSchema),
    defaultValues: paymentMethodDefaults(method),
  })
  const values = useWatch({ control: form.control }) as PaymentMethodFormValues
  const typeMeta = paymentTypeMeta[values.type]
  const labels = paymentFieldLabels(values.type)

  const onSubmit = form.handleSubmit((data) => {
    setServerError(null)
    save.mutate(
      { id: method?.id, body: toPaymentMethodRequest(data) },
      {
        onSuccess: () => {
          toast.success(method ? 'Método de pago actualizado.' : 'Método de pago agregado.')
          onDone()
        },
        onError: (error) => setServerError(applyServerErrors(form, error, fields)),
      },
    )
  })

  const linkField = (
    <FormField
      control={form.control}
      name="paymentLink"
      render={({ field }) => (
        <FormItem>
          <FormLabel>Link de pago {typeMeta.linkFirst ? '' : '(opcional)'}</FormLabel>
          <FormControl>
            <Input type="url" inputMode="url" placeholder="https://" maxLength={500} {...field} />
          </FormControl>
          <FormMessage />
        </FormItem>
      )}
    />
  )

  return (
    <Form {...form}>
      <form onSubmit={onSubmit} className="grid gap-5" noValidate>
        <DialogHeader>
          <DialogTitle>{method ? 'Editar método de pago' : 'Agregar método de pago'}</DialogTitle>
          <DialogDescription>
            Proporciona todos los datos para que el cliente pueda pagar correctamente sin tener que preguntar.
          </DialogDescription>
        </DialogHeader>

        {serverError && <Alert tone="destructive">{serverError}</Alert>}

        <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_20rem]">
          <div className="grid content-start gap-4">
            <div className="grid gap-4 sm:grid-cols-2">
              <FormField
                control={form.control}
                name="type"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Tipo de método</FormLabel>
                    <Select value={field.value} onValueChange={(v) => field.onChange(v as PaymentMethodType)}>
                      <FormControl>
                        <SelectTrigger className="w-full">
                          <SelectValue placeholder="Selecciona un tipo" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        {paymentTypes.map((type) => {
                          const meta = paymentTypeMeta[type]
                          return (
                            <SelectItem key={type} value={type}>
                              <meta.icon aria-hidden />
                              {meta.label}
                            </SelectItem>
                          )
                        })}
                      </SelectContent>
                    </Select>
                    <FormDescription className="text-xs">{typeMeta.description}</FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="name"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Nombre visible</FormLabel>
                    <FormControl>
                      <Input placeholder={typeMeta.namePlaceholder} autoFocus maxLength={150} {...field} />
                    </FormControl>
                    <FormDescription className="text-xs">Así lo nombrará tu vendedor.</FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            {typeMeta.linkFirst && linkField}

            <div className="grid gap-4 sm:grid-cols-2">
              <FormField
                control={form.control}
                name="accountNumber"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{labels.account} (opcional)</FormLabel>
                    <FormControl>
                      <Input placeholder={typeMeta.accountPlaceholder} maxLength={100} {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="accountAlias"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{labels.alias} (opcional)</FormLabel>
                    <FormControl>
                      <Input placeholder={typeMeta.aliasPlaceholder} maxLength={100} {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            <FormField
              control={form.control}
              name="instructions"
              render={({ field }) => (
                <FormItem>
                  <div className="flex items-center justify-between gap-2">
                    <FormLabel>Instrucciones de pago</FormLabel>
                    <CharCounter value={values.instructions ?? ''} max={PAYMENT_INSTRUCTIONS_MAX} />
                  </div>
                  <FormControl>
                    <Textarea rows={4} className="max-h-60" placeholder={typeMeta.instructionsPlaceholder} {...field} />
                  </FormControl>
                  <FormDescription className="text-xs">
                    Usa *asteriscos* para resaltar en negrita, como en WhatsApp.
                  </FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            {!typeMeta.linkFirst && linkField}

            <FormField
              control={form.control}
              name="isActive"
              render={({ field }) => (
                <FormItem className="flex flex-row items-center justify-between gap-4 rounded-lg border p-3">
                  <div className="space-y-0.5">
                    <FormLabel>Método activo</FormLabel>
                    <FormDescription className="text-xs">Tu vendedor solo ofrece los métodos activos.</FormDescription>
                  </div>
                  <FormControl>
                    <Switch checked={field.value} onCheckedChange={field.onChange} />
                  </FormControl>
                </FormItem>
              )}
            />
          </div>

          <div className="lg:sticky lg:top-0 lg:self-start">
            <p className="text-muted-foreground mb-2 text-xs font-medium tracking-wide uppercase">Vista previa</p>
            <WhatsAppPreview
              businessName={business.name}
              data={{
                type: values.type,
                name: values.name ?? '',
                instructions: values.instructions ?? '',
                accountNumber: values.accountNumber ?? '',
                accountAlias: values.accountAlias ?? '',
                paymentLink: values.paymentLink ?? '',
              }}
            />
          </div>
        </div>

        <DialogFooter>
          <Button type="button" variant="outline" onClick={onDone} disabled={save.isPending}>
            Cancelar
          </Button>
          <Button type="submit" disabled={save.isPending}>
            {save.isPending && <Loader2 className="animate-spin" aria-hidden />}
            {method ? 'Guardar cambios' : 'Agregar método'}
          </Button>
        </DialogFooter>
      </form>
    </Form>
  )
}
