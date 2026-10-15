import { zodResolver } from '@hookform/resolvers/zod'
import { Loader2, Save } from 'lucide-react'
import { useMemo } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { Button } from '@/components/ui/button'
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { ApiError } from '@/lib/api/client'
import { useUpdateOrder, type OrderDetail } from '../api'

const schema = z.object({
  shippingAddress: z.string().max(500, 'La dirección no puede superar 500 caracteres.'),
  paymentMethodName: z.string().max(150, 'El método de pago no puede superar 150 caracteres.'),
  notes: z.string().max(1000, 'Las notas no pueden superar 1000 caracteres.'),
})
type FormValues = z.infer<typeof schema>

/** Datos de entrega, pago y notas internas editables desde el panel. */
export function OrderAdminForm({ order }: { order: OrderDetail }) {
  const update = useUpdateOrder(order.id)
  const values = useMemo<FormValues>(
    () => ({
      shippingAddress: order.shippingAddress ?? '',
      paymentMethodName: order.paymentMethodName ?? '',
      notes: order.notes ?? '',
    }),
    [order.shippingAddress, order.paymentMethodName, order.notes],
  )
  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    values,
    // Si el pedido se actualiza en segundo plano no se pisan los cambios sin guardar
    resetOptions: { keepDirtyValues: true },
  })

  const onSubmit = form.handleSubmit((data) =>
    update.mutate(
      {
        shippingAddress: data.shippingAddress.trim() || null,
        paymentMethodName: data.paymentMethodName.trim() || null,
        notes: data.notes.trim() || null,
      },
      {
        onSuccess: (saved) =>
          form.reset({
            shippingAddress: saved.shippingAddress ?? '',
            paymentMethodName: saved.paymentMethodName ?? '',
            notes: saved.notes ?? '',
          }),
        onError: (error) => {
          if (!(error instanceof ApiError)) return
          for (const [field, message] of Object.entries(error.fieldErrors)) {
            if (field in values) form.setError(field as keyof FormValues, { message })
          }
        },
      },
    ),
  )

  return (
    <Form {...form}>
      <form onSubmit={onSubmit} className="space-y-4" noValidate>
        <FormField
          control={form.control}
          name="shippingAddress"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Dirección de entrega</FormLabel>
              <FormControl>
                <Textarea rows={3} placeholder="Calle, número, distrito y referencia" {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="paymentMethodName"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Método de pago</FormLabel>
              <FormControl>
                <Input placeholder="Yape, transferencia, contra entrega…" {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="notes"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Notas internas</FormLabel>
              <FormControl>
                <Textarea rows={3} placeholder="Detalles para tu equipo (el cliente no las ve)" {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <div className="flex justify-end gap-2">
          {form.formState.isDirty && (
            <Button type="button" variant="ghost" onClick={() => form.reset(values)} disabled={update.isPending}>
              Descartar
            </Button>
          )}
          <Button type="submit" disabled={!form.formState.isDirty || update.isPending}>
            {update.isPending ? <Loader2 className="animate-spin" /> : <Save />}
            Guardar cambios
          </Button>
        </div>
      </form>
    </Form>
  )
}
