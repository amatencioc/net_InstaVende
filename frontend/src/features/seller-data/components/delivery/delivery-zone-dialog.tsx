import { zodResolver } from '@hookform/resolvers/zod'
import { Loader2 } from 'lucide-react'
import { useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { toast } from 'sonner'
import { useSaveDeliveryZone, type DeliveryZone } from '../../api'
import {
  deliveryZoneDefaults,
  deliveryZoneFormSchema,
  toDeliveryZoneRequest,
  type CostMode,
  type DeliveryZoneFormValues,
} from '../../schemas'
import { applyServerErrors } from '../../utils'
import { CharCounter } from '../char-counter'
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
import { Switch } from '@/components/ui/switch'
import { Textarea } from '@/components/ui/textarea'
import { ApiError } from '@/lib/api/client'
import { useSession } from '@/lib/auth/session'
import { cn } from '@/lib/utils'

export interface DeliveryZoneDialogState {
  zone?: DeliveryZone
}

/** Crear o editar una zona de entrega. */
export function DeliveryZoneDialog({
  state,
  onOpenChange,
}: {
  state: DeliveryZoneDialogState | null
  onOpenChange: (open: boolean) => void
}) {
  return (
    <Dialog open={state !== null} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        {state && <DeliveryZoneForm zone={state.zone} onDone={() => onOpenChange(false)} />}
      </DialogContent>
    </Dialog>
  )
}

const costModes: { value: CostMode; label: string; hint: string }[] = [
  { value: 'fixed', label: 'Precio fijo', hint: 'Monto por envío' },
  { value: 'free', label: 'Gratis', hint: 'Sin costo' },
  { value: 'coordinate', label: 'A coordinar', hint: 'Se acuerda por chat' },
]

const fields = ['name', 'cost', 'description', 'isActive'] as const

function DeliveryZoneForm({ zone, onDone }: { zone?: DeliveryZone; onDone: () => void }) {
  const { business } = useSession()
  const save = useSaveDeliveryZone()
  const [serverError, setServerError] = useState<string | null>(null)
  const form = useForm<DeliveryZoneFormValues>({
    resolver: zodResolver(deliveryZoneFormSchema),
    defaultValues: deliveryZoneDefaults(zone),
  })
  const costMode = useWatch({ control: form.control, name: 'costMode' })
  const description = useWatch({ control: form.control, name: 'description' })

  const onSubmit = form.handleSubmit((values) => {
    setServerError(null)
    save.mutate(
      { id: zone?.id, body: toDeliveryZoneRequest(values) },
      {
        onSuccess: () => {
          toast.success(zone ? 'Zona actualizada.' : 'Zona agregada.')
          onDone()
        },
        onError: (error) => {
          // El nombre duplicado llega como conflicto (409): se muestra junto al campo
          if (error instanceof ApiError && error.status === 409) {
            form.setError('name', { type: 'server', message: error.message }, { shouldFocus: true })
            return
          }
          setServerError(applyServerErrors(form, error, fields))
        },
      },
    )
  })

  return (
    <Form {...form}>
      <form onSubmit={onSubmit} className="grid gap-5" noValidate>
        <DialogHeader>
          <DialogTitle>{zone ? 'Editar zona de entrega' : 'Agregar zona de entrega'}</DialogTitle>
          <DialogDescription>Tu vendedor informará el costo y las condiciones de envío de cada zona.</DialogDescription>
        </DialogHeader>

        {serverError && <Alert tone="destructive">{serverError}</Alert>}

        <FormField
          control={form.control}
          name="name"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Nombre de la zona</FormLabel>
              <FormControl>
                <Input placeholder="Lima Metropolitana" autoFocus maxLength={100} {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />

        <FormField
          control={form.control}
          name="costMode"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Costo de envío</FormLabel>
              <FormControl>
                <RadioGroup
                  value={field.value}
                  onValueChange={(v) => field.onChange(v as CostMode)}
                  className="grid grid-cols-3 gap-2"
                >
                  {costModes.map((mode) => (
                    <label
                      key={mode.value}
                      className={cn(
                        'hover:bg-accent/50 flex cursor-pointer flex-col gap-1 rounded-lg border p-3 text-sm transition-colors',
                        'has-[button[data-state=checked]]:border-primary has-[button[data-state=checked]]:bg-primary/5',
                      )}
                    >
                      <span className="flex items-center gap-2 font-medium">
                        <RadioGroupItem value={mode.value} />
                        {mode.label}
                      </span>
                      <span className="text-muted-foreground hidden text-xs sm:block">{mode.hint}</span>
                    </label>
                  ))}
                </RadioGroup>
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />

        {costMode === 'fixed' && (
          <FormField
            control={form.control}
            name="cost"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Monto</FormLabel>
                <div className="relative sm:max-w-48">
                  <span className="text-muted-foreground pointer-events-none absolute top-1/2 left-3 -translate-y-1/2 text-sm">
                    {business.currencySymbol}
                  </span>
                  <FormControl>
                    <Input inputMode="decimal" placeholder="10.00" className="pl-10 tabular-nums" {...field} />
                  </FormControl>
                </div>
                <FormMessage />
              </FormItem>
            )}
          />
        )}

        <FormField
          control={form.control}
          name="description"
          render={({ field }) => (
            <FormItem>
              <div className="flex items-center justify-between gap-2">
                <FormLabel>Descripción (opcional)</FormLabel>
                <CharCounter value={description} max={500} />
              </div>
              <FormControl>
                <Textarea
                  rows={3}
                  placeholder="Entrega en 24 a 48 horas. No llegamos a Ancón ni Pucusana."
                  className="max-h-48"
                  {...field}
                />
              </FormControl>
              <FormDescription>Plazos, distritos incluidos o excluidos, horarios de reparto…</FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />

        <FormField
          control={form.control}
          name="isActive"
          render={({ field }) => (
            <FormItem className="flex flex-row items-center justify-between gap-4 rounded-lg border p-3">
              <div className="space-y-0.5">
                <FormLabel>Zona activa</FormLabel>
                <FormDescription className="text-xs">Solo las zonas activas se ofrecen a los clientes.</FormDescription>
              </div>
              <FormControl>
                <Switch checked={field.value} onCheckedChange={field.onChange} />
              </FormControl>
            </FormItem>
          )}
        />

        <DialogFooter>
          <Button type="button" variant="outline" onClick={onDone} disabled={save.isPending}>
            Cancelar
          </Button>
          <Button type="submit" disabled={save.isPending}>
            {save.isPending && <Loader2 className="animate-spin" aria-hidden />}
            {zone ? 'Guardar cambios' : 'Agregar zona'}
          </Button>
        </DialogFooter>
      </form>
    </Form>
  )
}
