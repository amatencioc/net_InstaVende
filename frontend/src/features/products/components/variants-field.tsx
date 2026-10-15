import { Layers, Plus, Sparkles, Trash2 } from 'lucide-react'
import { useId, useState } from 'react'
import { useFieldArray, useFormContext } from 'react-hook-form'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { Switch } from '@/components/ui/switch'
import { useSession } from '@/lib/auth/session'
import { cn } from '@/lib/utils'
import { MAX_VARIANTS, newVariant, type ProductFormValues } from '../schemas'
import { NumberInput } from './number-input'

const ATTRIBUTE_SUGGESTIONS = ['Talla', 'Color', 'Material', 'Tamaño', 'Sabor', 'Modelo', 'Capacidad']

/** Variantes del producto (opción + valor + ajuste de precio + stock + SKU). */
export function VariantsField() {
  const form = useFormContext<ProductFormValues>()
  const { fields, append, remove } = useFieldArray({ control: form.control, name: 'variants', keyName: 'key' })
  const currency = useSession().business.currencySymbol
  const datalistId = useId()
  const rootError = form.formState.errors.variants?.root?.message ?? form.formState.errors.variants?.message

  return (
    <div className="space-y-4">
      <datalist id={datalistId}>
        {ATTRIBUTE_SUGGESTIONS.map((s) => (
          <option key={s} value={s} />
        ))}
      </datalist>

      <QuickAdd
        datalistId={datalistId}
        remaining={MAX_VARIANTS - fields.length}
        onAdd={(attribute, values) => {
          const current = form.getValues('variants')
          const existing = new Set(current.map((v) => `${v.attributeName.trim().toLowerCase()}::${v.attributeValue.trim().toLowerCase()}`))
          const fresh = values.filter((v) => !existing.has(`${attribute.toLowerCase()}::${v.toLowerCase()}`))
          if (fresh.length === 0) {
            toast.info('Esas variantes ya existen.')
            return
          }
          append(fresh.map((v) => newVariant(attribute, v)))
        }}
      />

      {fields.length === 0 ? (
        <div className="text-muted-foreground flex flex-col items-center gap-2 rounded-xl border border-dashed px-4 py-8 text-center text-sm">
          <Layers className="size-5" aria-hidden />
          <p>Sin variantes. Añade tallas, colores u otras opciones si tu producto las tiene.</p>
        </div>
      ) : (
        <div className="space-y-3">
          {/* Cabecera (solo escritorio) */}
          <div
            className="text-muted-foreground hidden grid-cols-[minmax(0,1fr)_minmax(0,1fr)_7.5rem_5.5rem_minmax(0,1fr)_3rem_2.25rem] gap-2 px-1 text-xs font-medium lg:grid"
            aria-hidden
          >
            <span>Opción</span>
            <span>Valor</span>
            <span>Ajuste de precio</span>
            <span>Stock</span>
            <span>SKU</span>
            <span>Activa</span>
            <span />
          </div>
          <ul className="space-y-3 lg:space-y-2" aria-label="Variantes">
            {fields.map((field, index) => (
              <li
                key={field.key}
                className={cn(
                  'grid grid-cols-2 gap-2 rounded-lg border p-3 sm:grid-cols-3',
                  'lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_7.5rem_5.5rem_minmax(0,1fr)_3rem_2.25rem] lg:items-start lg:border-0 lg:p-0',
                )}
              >
                <FormField
                  control={form.control}
                  name={`variants.${index}.attributeName`}
                  render={({ field: f }) => (
                    <FormItem className="gap-1">
                      <FormLabel className="text-xs lg:sr-only">Opción</FormLabel>
                      <FormControl>
                        <Input placeholder="Ej. Talla" list={datalistId} maxLength={50} {...f} />
                      </FormControl>
                      <FormMessage className="text-xs" />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name={`variants.${index}.attributeValue`}
                  render={({ field: f }) => (
                    <FormItem className="gap-1">
                      <FormLabel className="text-xs lg:sr-only">Valor</FormLabel>
                      <FormControl>
                        <Input placeholder="Ej. M" maxLength={100} {...f} />
                      </FormControl>
                      <FormMessage className="text-xs" />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name={`variants.${index}.priceModifier`}
                  render={({ field: f }) => (
                    <FormItem className="gap-1">
                      <FormLabel className="text-xs lg:sr-only">Ajuste de precio</FormLabel>
                      <FormControl>
                        <NumberInput
                          ref={f.ref}
                          name={f.name}
                          value={f.value}
                          onChange={(v) => f.onChange(v)}
                          onBlur={f.onBlur}
                          allowNegative
                          prefix={currency}
                          placeholder="0"
                          title="Se suma (o resta) al precio base"
                        />
                      </FormControl>
                      <FormMessage className="text-xs" />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name={`variants.${index}.stock`}
                  render={({ field: f }) => (
                    <FormItem className="gap-1">
                      <FormLabel className="text-xs lg:sr-only">Stock</FormLabel>
                      <FormControl>
                        <NumberInput
                          ref={f.ref}
                          name={f.name}
                          value={f.value}
                          onChange={(v) => f.onChange(v)}
                          onBlur={f.onBlur}
                          integer
                          placeholder="0"
                        />
                      </FormControl>
                      <FormMessage className="text-xs" />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name={`variants.${index}.sku`}
                  render={({ field: f }) => (
                    <FormItem className="gap-1">
                      <FormLabel className="text-xs lg:sr-only">SKU</FormLabel>
                      <FormControl>
                        <Input placeholder="Opcional" maxLength={64} {...f} />
                      </FormControl>
                      <FormMessage className="text-xs" />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name={`variants.${index}.isActive`}
                  render={({ field: f }) => (
                    <FormItem className="flex items-center gap-2 lg:h-9 lg:justify-center">
                      <FormControl>
                        <Switch checked={f.value} onCheckedChange={f.onChange} ref={f.ref} />
                      </FormControl>
                      <FormLabel className="text-xs font-normal lg:sr-only">Activa</FormLabel>
                    </FormItem>
                  )}
                />
                <div className="flex items-end justify-end lg:items-start">
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    className="text-muted-foreground hover:text-destructive"
                    aria-label={`Eliminar variante ${index + 1}`}
                    onClick={() => remove(index)}
                  >
                    <Trash2 />
                  </Button>
                </div>
              </li>
            ))}
          </ul>
        </div>
      )}

      {rootError && <p className="text-destructive text-sm">{rootError}</p>}

      <Button
        type="button"
        variant="outline"
        size="sm"
        disabled={fields.length >= MAX_VARIANTS}
        onClick={() => {
          const last = form.getValues('variants').at(-1)
          append(newVariant(last?.attributeName ?? ''), { shouldFocus: true, focusName: `variants.${fields.length}.${last ? 'attributeValue' : 'attributeName'}` })
        }}
      >
        <Plus /> Agregar variante
      </Button>
    </div>
  )
}

/** Generador rápido: "Color" + "Rojo, Azul, Negro" → tres variantes. */
function QuickAdd({
  datalistId,
  remaining,
  onAdd,
}: {
  datalistId: string
  remaining: number
  onAdd: (attribute: string, values: string[]) => void
}) {
  const [attribute, setAttribute] = useState('')
  const [values, setValues] = useState('')
  const attrId = useId()
  const valuesId = useId()

  const parsed = [...new Set(values.split(',').map((v) => v.trim()).filter(Boolean))].map((v) => v.slice(0, 100))
  const canAdd = attribute.trim().length > 0 && parsed.length > 0 && parsed.length <= remaining

  const submit = () => {
    if (!canAdd) return
    onAdd(attribute.trim().slice(0, 50), parsed)
    setValues('')
  }

  return (
    <div className="bg-muted/40 rounded-lg border p-3">
      <p className="mb-2 flex items-center gap-1.5 text-sm font-medium">
        <Sparkles className="text-primary size-4" aria-hidden /> Crear varias a la vez
      </p>
      <div className="grid gap-2 sm:grid-cols-[minmax(0,10rem)_minmax(0,1fr)_auto]">
        <div className="space-y-1">
          <label htmlFor={attrId} className="text-muted-foreground text-xs">
            Opción
          </label>
          <Input
            id={attrId}
            value={attribute}
            onChange={(e) => setAttribute(e.target.value)}
            list={datalistId}
            placeholder="Ej. Color"
            maxLength={50}
            className="bg-background"
          />
        </div>
        <div className="space-y-1">
          <label htmlFor={valuesId} className="text-muted-foreground text-xs">
            Valores separados por coma
          </label>
          <Input
            id={valuesId}
            value={values}
            onChange={(e) => setValues(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                e.preventDefault()
                submit()
              }
            }}
            placeholder="Ej. Blanco, Negro, Gris"
            className="bg-background"
          />
        </div>
        <div className="flex items-end">
          <Button type="button" variant="secondary" className="w-full sm:w-auto" disabled={!canAdd} onClick={submit}>
            <Plus /> Añadir {parsed.length > 1 ? parsed.length : ''}
          </Button>
        </div>
      </div>
      {parsed.length > remaining && (
        <p className="text-destructive mt-2 text-xs">Superarías el máximo de {MAX_VARIANTS} variantes.</p>
      )}
    </div>
  )
}
