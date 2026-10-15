import { zodResolver } from '@hookform/resolvers/zod'
import { useQuery } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import {
  BadgePercent,
  Clock,
  Headset,
  Megaphone,
  PauseCircle,
  PlayCircle,
  ShieldCheck,
  Truck,
} from 'lucide-react'
import { useEffect } from 'react'
import { useForm, useFormState, useWatch, type UseFormReturn } from 'react-hook-form'
import { Alert } from '@/components/shared/alert'
import { QueryError } from '@/components/shared/query-state'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Form, FormControl, FormDescription, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { Switch } from '@/components/ui/switch'
import { Textarea } from '@/components/ui/textarea'
import { ApiError } from '@/lib/api/client'
import { useIsAdmin } from '@/lib/auth/session'
import { formatMoney } from '@/lib/format'
import { cn } from '@/lib/utils'
import { sellerRulesQuery, useUpdateSellerRules, type SellerRules } from '../api'
import { rulesSchema, toRulesForm, toRulesRequest, type RulesFormValues } from '../schemas'
import { AffixNumberInput, CharCount, FormPageSkeleton, LabelRow, SectionCard } from './form-bits'
import { QuickRepliesCard } from './quick-replies'
import { UnsavedChangesBar } from './unsaved-changes'

const FORM_ID = 'seller-rules-form'

const HOURS_PRESETS = ['Lunes a Viernes 09:00-18:00', 'Lunes a Sábado 09:00-20:00', 'Todos los días 08:00-22:00']

/** Pestaña "Reglas y políticas". */
export function RulesPage() {
  const query = useQuery(sellerRulesQuery)
  if (query.isPending) return <FormPageSkeleton cards={4} />
  if (query.isError) return <QueryError error={query.error} onRetry={() => void query.refetch()} />
  return <RulesForm data={query.data} />
}

type SectionProps = { form: UseFormReturn<RulesFormValues> }

function RulesForm({ data }: { data: SellerRules }) {
  const isAdmin = useIsAdmin()
  const save = useUpdateSellerRules()
  const form = useForm<RulesFormValues>({
    resolver: zodResolver(rulesSchema),
    defaultValues: toRulesForm(data),
    mode: 'onTouched',
  })
  const { isDirty } = form.formState

  useEffect(() => {
    if (!form.formState.isDirty) form.reset(toRulesForm(data))
  }, [data, form])

  const onSubmit = form.handleSubmit((values) =>
    save.mutate(toRulesRequest(values), {
      onSuccess: (res) => form.reset(toRulesForm(res)),
      onError: (error) => {
        if (!(error instanceof ApiError)) return
        for (const [field, message] of Object.entries(error.fieldErrors))
          if (field in values) form.setError(field as keyof RulesFormValues, { message })
      },
    }),
  )

  return (
    <div className="flex flex-col gap-6">
      {!isAdmin && (
        <Alert tone="warning" title="Solo lectura">
          Solo un administrador del negocio puede modificar las reglas del vendedor.
        </Alert>
      )}

      <Form {...form}>
        <form id={FORM_ID} onSubmit={onSubmit} noValidate>
          <fieldset disabled={!isAdmin || save.isPending} className="min-w-0 space-y-6">
            <legend className="sr-only">Reglas y políticas del vendedor</legend>
            <BotStatusCard form={form} />
            <div className="grid gap-6 lg:grid-cols-2">
              <BusinessHoursSection form={form} saved={data} />
              <FreeShippingSection form={form} currencySymbol={data.currencySymbol} />
            </div>
            <PoliciesSection form={form} />
            <PromotionsSection form={form} />
            <NegotiationSection form={form} />
            <HandoffSection form={form} />
          </fieldset>
        </form>
      </Form>

      <QuickRepliesCard canEdit={isAdmin} />

      <UnsavedChangesBar
        dirty={isDirty}
        saving={save.isPending}
        onDiscard={() => form.reset()}
        formId={FORM_ID}
        disabled={!isAdmin}
      />
    </div>
  )
}

// ── Estado del bot ──────────────────────────────────────────────────────────

function BotStatusCard({ form }: SectionProps) {
  const isActive = useWatch({ control: form.control, name: 'isActive' })
  const botName = useWatch({ control: form.control, name: 'botName' })
  const StatusIcon = isActive ? PlayCircle : PauseCircle

  return (
    <Card
      className={cn(
        'gap-0 overflow-hidden border-l-4 py-0 transition-colors',
        isActive ? 'border-l-success' : 'border-l-warning',
      )}
    >
      <FormField
        control={form.control}
        name="isActive"
        render={({ field }) => (
          <FormItem
            className={cn(
              'flex flex-col gap-4 p-5 sm:flex-row sm:items-center sm:p-6',
              isActive ? 'bg-success/5' : 'bg-warning/10',
            )}
          >
            <div
              className={cn(
                'flex size-12 shrink-0 items-center justify-center rounded-full',
                isActive ? 'bg-success/15 text-success' : 'bg-warning/20 text-warning',
              )}
            >
              <StatusIcon className="size-6" aria-hidden />
            </div>
            <div className="min-w-0 flex-1 space-y-1">
              <FormLabel className="font-display text-lg font-semibold">
                {isActive ? 'Tu vendedor está activo' : 'Tu vendedor está pausado'}
              </FormLabel>
              <FormDescription>
                {isActive
                  ? 'Responde automáticamente a tus clientes en todos los canales conectados.'
                  : 'No responderá a ningún cliente hasta que lo actives. Tu equipo puede seguir atendiendo desde la bandeja.'}
              </FormDescription>
            </div>
            <div className="flex items-center gap-3 self-end sm:self-center">
              <span className={cn('text-sm font-medium', isActive ? 'text-success' : 'text-warning')} aria-hidden>
                {isActive ? 'Activo' : 'Pausado'}
              </span>
              <FormControl>
                <Switch
                  checked={field.value}
                  onCheckedChange={field.onChange}
                  className="origin-right scale-150"
                  aria-label="Vendedor activo"
                />
              </FormControl>
            </div>
          </FormItem>
        )}
      />
      <div className="border-t p-5 sm:px-6">
        <FormField
          control={form.control}
          name="botName"
          render={({ field }) => (
            <FormItem className="max-w-md">
              <LabelRow aside={<CharCount value={botName} max={60} />}>
                <FormLabel>Nombre interno del bot</FormLabel>
              </LabelRow>
              <FormControl>
                <Input maxLength={60} placeholder="Asistente" {...field} />
              </FormControl>
              <FormDescription>
                Se usa si no defines un nombre en{' '}
                <Link to="/seller/personality" className="text-primary font-medium hover:underline">
                  Personalidad
                </Link>
                .
              </FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />
      </div>
    </Card>
  )
}

// ── Horario ─────────────────────────────────────────────────────────────────

const hoursStatusMeta = {
  Open: { label: 'Abierto ahora', className: 'bg-success/15 text-success' },
  Closed: { label: 'Cerrado ahora', className: 'bg-muted text-muted-foreground' },
  Unknown: { label: 'No reconocido', className: 'bg-warning/15 text-warning' },
} as const

function BusinessHoursSection({ form, saved }: SectionProps & { saved: SellerRules }) {
  const value = useWatch({ control: form.control, name: 'businessHours' })
  const { dirtyFields } = useFormState({ control: form.control, name: 'businessHours' })
  const showStatus = !dirtyFields.businessHours && !!saved.businessHours
  const status = hoursStatusMeta[saved.hoursStatus]

  return (
    <SectionCard
      icon={Clock}
      title="Horario de atención"
      description="Cuándo atiende tu equipo humano. El vendedor lo usa al derivar fuera de horario."
      action={showStatus ? <Badge className={status.className}>{status.label}</Badge> : undefined}
    >
      <FormField
        control={form.control}
        name="businessHours"
        render={({ field }) => (
          <FormItem>
            <LabelRow aside={<CharCount value={value} max={200} />}>
              <FormLabel>Horario</FormLabel>
            </LabelRow>
            <FormControl>
              <Input maxLength={200} placeholder="Lunes a Viernes 09:00-18:00" {...field} />
            </FormControl>
            <FormDescription>Formato sugerido: días y rango de horas, p. ej. "Lunes a Viernes 09:00-18:00".</FormDescription>
            <FormMessage />
          </FormItem>
        )}
      />
      <div className="flex flex-wrap gap-2">
        {HOURS_PRESETS.map((preset) => (
          <Button
            key={preset}
            type="button"
            variant="outline"
            size="xs"
            onClick={() => form.setValue('businessHours', preset, { shouldDirty: true, shouldValidate: true })}
          >
            {preset}
          </Button>
        ))}
      </div>
      {showStatus && saved.hoursStatus === 'Unknown' && (
        <Alert tone="warning">
          No pudimos interpretar el horario guardado. Usa el formato sugerido para que el vendedor sepa si estás
          disponible.
        </Alert>
      )}
    </SectionCard>
  )
}

// ── Envío gratis ────────────────────────────────────────────────────────────

function FreeShippingSection({ form, currencySymbol }: SectionProps & { currencySymbol: string }) {
  const threshold = useWatch({ control: form.control, name: 'freeShippingThreshold' })
  const enabled = Number.isFinite(threshold) && threshold > 0

  return (
    <SectionCard
      icon={Truck}
      title="Envío gratis"
      description="Monto mínimo de compra a partir del cual el vendedor ofrece envío gratis."
      action={<Badge variant={enabled ? 'default' : 'secondary'}>{enabled ? 'Activo' : 'Desactivado'}</Badge>}
    >
      <FormField
        control={form.control}
        name="freeShippingThreshold"
        render={({ field }) => (
          <FormItem>
            <FormLabel>Envío gratis desde</FormLabel>
            <FormControl>
              <AffixNumberInput
                prefix={currencySymbol}
                value={field.value}
                onChange={field.onChange}
                onBlur={field.onBlur}
                name={field.name}
                ref={field.ref}
                min={0}
                step="0.01"
                className="max-w-56"
              />
            </FormControl>
            <FormDescription>
              {enabled
                ? `Pedidos desde ${formatMoney(threshold, currencySymbol)} tendrán envío gratis.`
                : 'Con 0 el vendedor no ofrece envío gratis por monto.'}
            </FormDescription>
            <FormMessage />
          </FormItem>
        )}
      />
    </SectionCard>
  )
}

// ── Políticas ───────────────────────────────────────────────────────────────

function PoliciesSection({ form }: SectionProps) {
  const [returns, warranty, shipping] = useWatch({
    control: form.control,
    name: ['returnPolicy', 'warrantyPolicy', 'shippingTimes'],
  })
  return (
    <SectionCard
      icon={ShieldCheck}
      title="Políticas"
      description="Si una política queda vacía, el vendedor deriva esas consultas a un asesor en lugar de inventar."
    >
      <div className="grid gap-5 md:grid-cols-2">
        <FormField
          control={form.control}
          name="returnPolicy"
          render={({ field }) => (
            <FormItem>
              <LabelRow aside={<CharCount value={returns} max={2000} />}>
                <FormLabel>Cambios y devoluciones</FormLabel>
              </LabelRow>
              <FormControl>
                <Textarea
                  rows={4}
                  maxLength={2000}
                  placeholder="Ej. Aceptamos cambios dentro de los 7 días con la etiqueta puesta y la boleta."
                  {...field}
                />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="warrantyPolicy"
          render={({ field }) => (
            <FormItem>
              <LabelRow aside={<CharCount value={warranty} max={2000} />}>
                <FormLabel>Garantía</FormLabel>
              </LabelRow>
              <FormControl>
                <Textarea
                  rows={4}
                  maxLength={2000}
                  placeholder="Ej. 6 meses de garantía por defectos de fábrica."
                  {...field}
                />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
      </div>
      <FormField
        control={form.control}
        name="shippingTimes"
        render={({ field }) => (
          <FormItem>
            <LabelRow aside={<CharCount value={shipping} max={1000} />}>
              <FormLabel>Tiempos de envío</FormLabel>
            </LabelRow>
            <FormControl>
              <Textarea
                rows={3}
                maxLength={1000}
                placeholder="Ej. Lima: 24 a 48 horas. Provincias: 3 a 5 días hábiles."
                {...field}
              />
            </FormControl>
            <FormDescription>
              Los costos por zona se configuran en{' '}
              <Link to="/seller/delivery" className="text-primary font-medium hover:underline">
                Entrega
              </Link>
              .
            </FormDescription>
            <FormMessage />
          </FormItem>
        )}
      />
    </SectionCard>
  )
}

// ── Promociones ─────────────────────────────────────────────────────────────

function PromotionsSection({ form }: SectionProps) {
  const value = useWatch({ control: form.control, name: 'activePromotions' })
  return (
    <SectionCard
      icon={Megaphone}
      title="Promociones vigentes"
      description="El vendedor las menciona cuando encajan con lo que busca el cliente."
    >
      <FormField
        control={form.control}
        name="activePromotions"
        render={({ field }) => (
          <FormItem>
            <LabelRow aside={<CharCount value={value} max={2000} />}>
              <FormLabel>Promociones</FormLabel>
            </LabelRow>
            <FormControl>
              <Textarea
                rows={4}
                maxLength={2000}
                placeholder={'Ej. 2x1 en polos básicos hasta el domingo.\n10 % de descuento pagando con transferencia.'}
                {...field}
              />
            </FormControl>
            <FormDescription>Escribe una promoción por línea e incluye su vigencia. Déjalo vacío si no hay promociones.</FormDescription>
            <FormMessage />
          </FormItem>
        )}
      />
    </SectionCard>
  )
}

// ── Negociación ─────────────────────────────────────────────────────────────

const percentFields = [
  { name: 'discountLevel1', label: 'Nivel 1' },
  { name: 'discountLevel2', label: 'Nivel 2' },
  { name: 'discountLevel3', label: 'Nivel 3' },
  { name: 'maxDiscountPercent', label: 'Descuento máximo' },
  { name: 'loyaltyDiscount', label: 'Fidelidad' },
  { name: 'bundleDiscount', label: 'Combo' },
] as const

const fmtPct = (v: number) => (Number.isFinite(v) ? `${v} %` : '—')

function NegotiationSection({ form }: SectionProps) {
  const [l1, l2, l3, maxPct, bundle] = useWatch({
    control: form.control,
    name: ['discountLevel1', 'discountLevel2', 'discountLevel3', 'maxDiscountPercent', 'bundleDiscount'],
  })

  const steps = [
    { title: '1.ª insistencia', detail: `Ofrece valor sin descontar: envío gratis, una promoción o un combo (${fmtPct(bundle)}).` },
    { title: '2.ª insistencia', detail: `Aplica el nivel 1: ${fmtPct(l1)}.` },
    { title: '3.ª insistencia', detail: `Sube al nivel 2: ${fmtPct(l2)}.` },
    { title: '4.ª insistencia', detail: `Último nivel: ${fmtPct(l3)}. Nunca supera ${fmtPct(maxPct)}.` },
  ]

  return (
    <SectionCard
      icon={BadgePercent}
      title="Negociación y descuentos"
      description="Hasta dónde puede rebajar tu vendedor cuando un cliente pide descuento."
    >
      <ol className="grid gap-2 sm:grid-cols-2 xl:grid-cols-4" aria-label="Cómo escala el vendedor los descuentos">
        {steps.map((s, i) => (
          <li key={s.title} className="bg-muted/50 relative rounded-lg border p-3">
            <div className="flex items-center gap-2">
              <span className="bg-primary text-primary-foreground flex size-5 items-center justify-center rounded-full text-[11px] font-semibold">
                {i + 1}
              </span>
              <span className="text-sm font-medium">{s.title}</span>
            </div>
            <p className="text-muted-foreground mt-1.5 text-xs leading-relaxed">{s.detail}</p>
          </li>
        ))}
      </ol>

      <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
        {percentFields.slice(0, 4).map((f) => (
          <PercentField key={f.name} form={form} name={f.name} label={f.label} />
        ))}
      </div>
      <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
        <PercentField
          form={form}
          name="loyaltyDiscount"
          label="Fidelidad"
          description="Para clientes que ya te compraron."
        />
        <PercentField form={form} name="bundleDiscount" label="Combo" description="Al llevar varios productos." />
      </div>
      <p className="text-muted-foreground text-xs">
        Los niveles deben ser ascendentes y ninguno puede superar el descuento máximo. Si el cliente afirma ser dueño o
        empleado para pedir más, el vendedor no cede y ofrece hablar con un asesor.
      </p>
    </SectionCard>
  )
}

function PercentField({
  form,
  name,
  label,
  description,
}: SectionProps & { name: (typeof percentFields)[number]['name']; label: string; description?: string }) {
  return (
    <FormField
      control={form.control}
      name={name}
      render={({ field }) => (
        <FormItem>
          <FormLabel>{label}</FormLabel>
          <FormControl>
            <AffixNumberInput
              suffix="%"
              value={field.value}
              onChange={field.onChange}
              onBlur={field.onBlur}
              name={field.name}
              ref={field.ref}
              min={0}
              max={100}
              step="0.5"
            />
          </FormControl>
          {description && <FormDescription className="text-xs">{description}</FormDescription>}
          <FormMessage className="text-xs" />
        </FormItem>
      )}
    />
  )
}

// ── Derivación ──────────────────────────────────────────────────────────────

function HandoffSection({ form }: SectionProps) {
  const [enabled, phrase, fallback] = useWatch({
    control: form.control,
    name: ['enableHandoff', 'handoffTriggerPhrase', 'fallbackMessage'],
  })
  return (
    <SectionCard
      icon={Headset}
      title="Derivación y respaldo"
      description="Atajos para pasar a un asesor y el mensaje que se envía cuando el vendedor no puede responder."
    >
      <FormField
        control={form.control}
        name="enableHandoff"
        render={({ field }) => (
          <FormItem className="flex items-start justify-between gap-4 rounded-lg border p-3">
            <div className="space-y-1">
              <FormLabel>Derivación inmediata por frase</FormLabel>
              <FormDescription className="text-xs">
                Si el cliente escribe la frase disparadora, la conversación pasa a un asesor al instante, sin usar IA.
              </FormDescription>
            </div>
            <FormControl>
              <Switch checked={field.value} onCheckedChange={field.onChange} />
            </FormControl>
          </FormItem>
        )}
      />
      <FormField
        control={form.control}
        name="handoffTriggerPhrase"
        render={({ field }) => (
          <FormItem className={cn(!enabled && 'opacity-60')}>
            <LabelRow aside={<CharCount value={phrase} max={100} />}>
              <FormLabel>Frase disparadora</FormLabel>
            </LabelRow>
            <FormControl>
              <Input maxLength={100} placeholder="Ej. hablar con un asesor" {...field} disabled={!enabled} />
            </FormControl>
            <FormDescription>
              El vendedor también deriva en las situaciones definidas en{' '}
              <Link to="/seller/personality" className="text-primary font-medium hover:underline">
                Personalidad
              </Link>
              .
            </FormDescription>
            <FormMessage />
          </FormItem>
        )}
      />
      <FormField
        control={form.control}
        name="fallbackMessage"
        render={({ field }) => (
          <FormItem>
            <LabelRow aside={<CharCount value={fallback} max={500} />}>
              <FormLabel>
                Mensaje de respaldo <span className="text-destructive">*</span>
              </FormLabel>
            </LabelRow>
            <FormControl>
              <Textarea rows={2} maxLength={500} {...field} />
            </FormControl>
            <FormDescription>Se envía si la IA no está disponible o falla al generar una respuesta.</FormDescription>
            <FormMessage />
          </FormItem>
        )}
      />
    </SectionCard>
  )
}

