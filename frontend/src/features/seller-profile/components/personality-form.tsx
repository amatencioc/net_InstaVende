import { zodResolver } from '@hookform/resolvers/zod'
import { useQuery } from '@tanstack/react-query'
import {
  Building2,
  ChevronDown,
  Hand,
  Headset,
  MessageSquareHeart,
  MessagesSquare,
  Sparkles,
  Users,
  Wand2,
} from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { useForm, useWatch, type UseFormReturn } from 'react-hook-form'
import { Alert } from '@/components/shared/alert'
import { useConfirm } from '@/components/shared/confirm-dialog'
import { QueryError } from '@/components/shared/query-state'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Form, FormControl, FormDescription, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Slider } from '@/components/ui/slider'
import { Switch } from '@/components/ui/switch'
import { Textarea } from '@/components/ui/textarea'
import { ApiError } from '@/lib/api/client'
import { useIsAdmin } from '@/lib/auth/session'
import { cn } from '@/lib/utils'
import { sellerProfileQuery, useUpdateSellerProfile, type SellerProfileResponse, type SellerProfileTemplate } from '../api'
import {
  genderOptions,
  profileSchema,
  responseLengthHints,
  responseLengthLabels,
  toProfileForm,
  toProfileRequest,
  type ProfileFormValues,
} from '../schemas'
import { nowTime } from '../utils'
import { ChatBubble, ChatHeader, ChatNotice, ChatSurface } from './chat'
import { EmojiPalettePicker } from './emoji-palette-picker'
import { CharCount, FormPageSkeleton, LabelRow, SectionCard } from './form-bits'
import { TagInput } from './tag-input'
import { UnsavedChangesBar } from './unsaved-changes'

const FORM_ID = 'seller-personality-form'

/** Pestaña "Personalidad": carga la configuración y muestra el formulario. */
export function PersonalityPage() {
  const query = useQuery(sellerProfileQuery)
  if (query.isPending) return <FormPageSkeleton cards={4} aside />
  if (query.isError) return <QueryError error={query.error} onRetry={() => void query.refetch()} />
  return <PersonalityForm data={query.data} />
}

function PersonalityForm({ data }: { data: SellerProfileResponse }) {
  const isAdmin = useIsAdmin()
  const confirm = useConfirm()
  const save = useUpdateSellerProfile()
  const form = useForm<ProfileFormValues>({
    resolver: zodResolver(profileSchema),
    defaultValues: toProfileForm(data.profile),
    mode: 'onTouched',
  })
  const { isDirty } = form.formState

  // Si otro usuario guarda y la consulta se refresca, se actualiza el formulario solo cuando no hay cambios locales.
  useEffect(() => {
    if (!form.formState.isDirty) form.reset(toProfileForm(data.profile))
  }, [data, form])

  const onSubmit = form.handleSubmit((values) =>
    save.mutate(toProfileRequest(values), {
      onSuccess: (res) => form.reset(toProfileForm(res.profile)),
      onError: (error) => {
        if (!(error instanceof ApiError)) return
        for (const [field, message] of Object.entries(error.fieldErrors))
          if (field in values) form.setError(field as keyof ProfileFormValues, { message })
      },
    }),
  )

  async function applyTemplate(t: SellerProfileTemplate) {
    const current = form.getValues()
    if (current.communicationStyle.trim() || current.salesStyle.trim()) {
      const ok = await confirm({
        title: `Aplicar plantilla "${t.name}"`,
        description: 'Se reemplazarán el estilo de comunicación, el estilo de ventas, la longitud de respuesta y el uso de emojis. Podrás ajustarlos antes de guardar.',
        confirmLabel: 'Aplicar plantilla',
      })
      if (!ok) return
    }
    const opts = { shouldDirty: true, shouldValidate: true } as const
    form.setValue('communicationStyle', t.communicationStyle, opts)
    form.setValue('salesStyle', t.salesStyle, opts)
    form.setValue('responseLength', t.responseLength, opts)
    form.setValue('useEmojis', t.useEmojis, opts)
    form.setValue('emojiPalette', [...t.emojiPalette], opts)
    form.setValue('useOpeningPunctuation', t.useOpeningPunctuation, opts)
  }

  return (
    <div className="flex flex-col gap-6">
      {!isAdmin && (
        <Alert tone="warning" title="Solo lectura">
          Solo un administrador del negocio puede modificar la personalidad del vendedor.
        </Alert>
      )}
      {data.profile.updatedAt == null && isAdmin && (
        <Alert tone="info" title="Configura a tu vendedor">
          Te mostramos valores sugeridos. Revísalos, ajusta el tono a tu marca y guarda para empezar a usarlos.
        </Alert>
      )}

      <Form {...form}>
        <form id={FORM_ID} onSubmit={onSubmit} noValidate>
          <fieldset disabled={!isAdmin || save.isPending} className="grid min-w-0 gap-6 lg:grid-cols-[minmax(0,1fr)_340px]">
            <legend className="sr-only">Personalidad del vendedor</legend>
            <div className="min-w-0 space-y-6">
              <BasicInfoSection form={form} countries={data.countries} />
              <AudienceSection form={form} />
              <PersonalitySection form={form} templates={data.templates} onTemplate={applyTemplate} disabled={!isAdmin} />
              <WelcomeSection form={form} />
              <MessagesSection form={form} />
              <HandoffSection form={form} />
            </div>
            <aside className="min-w-0" aria-label="Vista previa">
              <LivePreview form={form} />
            </aside>
          </fieldset>
        </form>
      </Form>

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

type SectionProps = { form: UseFormReturn<ProfileFormValues> }

// ── 1. Información básica ───────────────────────────────────────────────────

function BasicInfoSection({ form, countries }: SectionProps & { countries: string[] }) {
  const description = useWatch({ control: form.control, name: 'businessDescription' })
  const name = useWatch({ control: form.control, name: 'vendorName' })
  return (
    <SectionCard icon={Building2} title="Información básica" description="Cómo se presenta tu vendedor y dónde opera.">
      <div className="grid gap-5 sm:grid-cols-2">
        <FormField
          control={form.control}
          name="vendorName"
          render={({ field }) => (
            <FormItem>
              <LabelRow aside={<CharCount value={name} max={60} />}>
                <FormLabel>Nombre del vendedor</FormLabel>
              </LabelRow>
              <FormControl>
                <Input placeholder="Ej. Sofía" maxLength={60} autoComplete="off" {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="country"
          render={({ field }) => (
            <FormItem>
              <FormLabel>País de operación</FormLabel>
              <Select value={field.value} onValueChange={field.onChange} disabled={field.disabled}>
                <FormControl>
                  <SelectTrigger className="w-full" onBlur={field.onBlur}>
                    <SelectValue placeholder="Selecciona un país" />
                  </SelectTrigger>
                </FormControl>
                <SelectContent>
                  {countries.map((c) => (
                    <SelectItem key={c} value={c}>
                      {c}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <FormDescription>Define modismos, horario y zona horaria del vendedor.</FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />
      </div>

      <FormField
        control={form.control}
        name="vendorGender"
        render={({ field }) => (
          <FormItem>
            <FormLabel>Género del vendedor</FormLabel>
            <FormControl>
              <RadioGroup
                value={field.value || 'none'}
                onValueChange={(v) => field.onChange(v === 'none' ? '' : v)}
                className="grid grid-cols-1 gap-2 sm:grid-cols-3"
              >
                {genderOptions.map((o) => {
                  const value = o.value || 'none'
                  const id = `gender-${value}`
                  return (
                    <label
                      key={value}
                      htmlFor={id}
                      className={cn(
                        'hover:bg-muted/60 flex cursor-pointer items-center gap-2.5 rounded-lg border px-3 py-2.5 text-sm transition-colors',
                        (field.value || 'none') === value && 'border-primary bg-primary/5',
                      )}
                    >
                      <RadioGroupItem id={id} value={value} />
                      {o.label}
                    </label>
                  )
                })}
              </RadioGroup>
            </FormControl>
            <FormDescription>Ajusta la concordancia de género en las respuestas (p. ej. "encantado" o "encantada").</FormDescription>
          </FormItem>
        )}
      />

      <FormField
        control={form.control}
        name="businessDescription"
        render={({ field }) => (
          <FormItem>
            <LabelRow aside={<CharCount value={description} max={1000} />}>
              <FormLabel>Descripción del negocio</FormLabel>
            </LabelRow>
            <FormControl>
              <Textarea
                rows={4}
                maxLength={1000}
                placeholder="Ej. Tienda online de ropa urbana para jóvenes. Vendemos polos, casacas y zapatillas con envíos a todo el país."
                {...field}
              />
            </FormControl>
            <FormDescription>Qué vendes, qué te diferencia y cualquier dato clave del negocio.</FormDescription>
            <FormMessage />
          </FormItem>
        )}
      />
    </SectionCard>
  )
}

// ── 2. Audiencia y reglas ───────────────────────────────────────────────────

function AudienceSection({ form }: SectionProps) {
  const audience = useWatch({ control: form.control, name: 'targetAudience' })
  const rules = useWatch({ control: form.control, name: 'rules' })
  const rulesRef = useRef<HTMLTextAreaElement | null>(null)

  function insertRule(prefix: 'SIEMPRE' | 'NUNCA') {
    const current = form.getValues('rules')
    const next = `${current}${current && !current.endsWith('\n') ? '\n' : ''}${prefix} `
    form.setValue('rules', next.slice(0, 2000), { shouldDirty: true })
    requestAnimationFrame(() => {
      const el = rulesRef.current
      if (!el) return
      el.focus()
      el.setSelectionRange(el.value.length, el.value.length)
    })
  }

  return (
    <SectionCard icon={Users} title="Audiencia y reglas" description="A quién le vendes y qué debe respetar siempre tu vendedor.">
      <FormField
        control={form.control}
        name="targetAudience"
        render={({ field }) => (
          <FormItem>
            <LabelRow aside={<CharCount value={audience} max={300} />}>
              <FormLabel>¿A quién le vendes?</FormLabel>
            </LabelRow>
            <FormControl>
              <Input maxLength={300} placeholder="Ej. Jóvenes de 18 a 30 años interesados en moda urbana" {...field} />
            </FormControl>
            <FormMessage />
          </FormItem>
        )}
      />
      <FormField
        control={form.control}
        name="rules"
        render={({ field }) => (
          <FormItem>
            <LabelRow aside={<CharCount value={rules} max={2000} />}>
              <FormLabel>Reglas para tu vendedor</FormLabel>
            </LabelRow>
            <FormControl>
              <Textarea
                rows={5}
                maxLength={2000}
                placeholder={'SIEMPRE confirma la talla antes de tomar el pedido.\nNUNCA ofrezcas descuentos fuera de los niveles configurados.'}
                {...field}
                ref={(el) => {
                  field.ref(el)
                  rulesRef.current = el
                }}
              />
            </FormControl>
            <div className="flex flex-wrap items-center gap-2">
              <span className="text-muted-foreground text-xs">Añadir regla:</span>
              <Button type="button" size="xs" variant="outline" onClick={() => insertRule('SIEMPRE')}>
                + SIEMPRE…
              </Button>
              <Button type="button" size="xs" variant="outline" onClick={() => insertRule('NUNCA')}>
                + NUNCA…
              </Button>
            </div>
            <FormDescription>
              Consejo: escribe cada regla en una línea usando términos absolutos como <strong>SIEMPRE</strong> y{' '}
              <strong>NUNCA</strong>; así el vendedor las sigue con más precisión.
            </FormDescription>
            <FormMessage />
          </FormItem>
        )}
      />
    </SectionCard>
  )
}

// ── 3. Personalidad ─────────────────────────────────────────────────────────

function PersonalitySection({
  form,
  templates,
  onTemplate,
  disabled,
}: SectionProps & { templates: SellerProfileTemplate[]; onTemplate: (t: SellerProfileTemplate) => void; disabled: boolean }) {
  const communication = useWatch({ control: form.control, name: 'communicationStyle' })
  const sales = useWatch({ control: form.control, name: 'salesStyle' })
  const useEmojis = useWatch({ control: form.control, name: 'useEmojis' })

  return (
    <SectionCard
      icon={Sparkles}
      title="Personalidad"
      description="El tono y la forma de vender. Empieza con una plantilla y ajústala a tu marca."
      action={
        // modal={false}: el menú abre un diálogo de confirmación y así Radix no deja bloqueado el puntero.
        <DropdownMenu modal={false}>
          <DropdownMenuTrigger asChild>
            <Button type="button" variant="outline" size="sm" disabled={disabled}>
              <Wand2 /> Plantillas <ChevronDown className="opacity-60" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-72">
            <DropdownMenuLabel>Estilos predefinidos</DropdownMenuLabel>
            <DropdownMenuSeparator />
            {templates.map((t) => (
              <DropdownMenuItem key={t.id} onSelect={() => onTemplate(t)} className="flex-col items-start gap-0.5 py-2">
                <span className="font-medium">
                  {t.name} {t.emojiPalette.slice(0, 3).join('')}
                </span>
                <span className="text-muted-foreground text-xs leading-snug">{t.description}</span>
              </DropdownMenuItem>
            ))}
          </DropdownMenuContent>
        </DropdownMenu>
      }
    >
      <div className="grid gap-5 md:grid-cols-2">
        <FormField
          control={form.control}
          name="communicationStyle"
          render={({ field }) => (
            <FormItem>
              <LabelRow aside={<CharCount value={communication} max={200} />}>
                <FormLabel>Estilo de comunicación</FormLabel>
              </LabelRow>
              <FormControl>
                <Textarea rows={4} maxLength={200} placeholder="Ej. Hablo de tú, con calidez y frases cortas." {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="salesStyle"
          render={({ field }) => (
            <FormItem>
              <LabelRow aside={<CharCount value={sales} max={210} />}>
                <FormLabel>Estilo de ventas</FormLabel>
              </LabelRow>
              <FormControl>
                <Textarea rows={4} maxLength={210} placeholder="Ej. Recomiendo sin presionar y sugiero complementos." {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
      </div>

      <FormField
        control={form.control}
        name="responseLength"
        render={({ field }) => (
          <FormItem>
            <LabelRow
              aside={
                <Badge variant="secondary" className="bg-accent text-accent-foreground">
                  {responseLengthLabels[field.value - 1]}
                </Badge>
              }
            >
              <FormLabel>Longitud de respuesta</FormLabel>
            </LabelRow>
            <FormControl>
              <Slider
                min={1}
                max={5}
                step={1}
                value={[field.value]}
                onValueChange={([v]) => v != null && field.onChange(v)}
                disabled={field.disabled}
                aria-label="Longitud de respuesta"
                className="py-2"
              />
            </FormControl>
            <div className="grid grid-cols-5 text-center text-[11px] leading-tight sm:text-xs">
              {responseLengthLabels.map((label, i) => (
                <button
                  key={label}
                  type="button"
                  onClick={() => form.setValue('responseLength', i + 1, { shouldDirty: true })}
                  className={cn(
                    'text-muted-foreground hover:text-foreground rounded px-0.5 py-1 transition-colors',
                    i === 0 && 'text-left',
                    i === 4 && 'text-right',
                    field.value === i + 1 && 'text-foreground font-medium',
                  )}
                >
                  {label}
                </button>
              ))}
            </div>
            <FormDescription>{responseLengthHints[field.value - 1]}</FormDescription>
          </FormItem>
        )}
      />

      <div className="grid gap-3 sm:grid-cols-2">
        <SwitchField
          form={form}
          name="useEmojis"
          label="Usar emojis"
          description="Le da un tono más cercano a los mensajes."
        />
        <SwitchField
          form={form}
          name="useOpeningPunctuation"
          label="Usar signos de apertura (¿ ¡)"
          description="Desactívalo para un estilo de chat más informal."
        />
      </div>

      <FormField
        control={form.control}
        name="emojiPalette"
        render={({ field }) => (
          <FormItem>
            <FormLabel>Paleta de emojis permitidos</FormLabel>
            <FormControl>
              <EmojiPalettePicker value={field.value} onChange={field.onChange} disabled={!useEmojis || field.disabled} />
            </FormControl>
            <FormDescription>
              {useEmojis
                ? 'Si eliges emojis, el vendedor solo usará los de esta paleta.'
                : 'Activa "Usar emojis" para elegir una paleta.'}
            </FormDescription>
            <FormMessage />
          </FormItem>
        )}
      />

      <FormField
        control={form.control}
        name="wordsToAvoid"
        render={({ field }) => (
          <FormItem>
            <FormLabel>Palabras a evitar</FormLabel>
            <FormControl>
              <TagInput
                value={field.value}
                onChange={field.onChange}
                disabled={field.disabled}
                maxItems={50}
                maxLength={60}
                itemLabel="palabra"
                placeholder="Ej. barato, urgente, imposible"
              />
            </FormControl>
            <FormMessage />
          </FormItem>
        )}
      />
    </SectionCard>
  )
}

function SwitchField({
  form,
  name,
  label,
  description,
}: SectionProps & { name: 'useEmojis' | 'useOpeningPunctuation' | 'autoPauseOnHandoff'; label: string; description: string }) {
  return (
    <FormField
      control={form.control}
      name={name}
      render={({ field }) => (
        <FormItem className="flex items-start justify-between gap-4 rounded-lg border p-3">
          <div className="space-y-1">
            <FormLabel>{label}</FormLabel>
            <FormDescription className="text-xs">{description}</FormDescription>
          </div>
          <FormControl>
            <Switch checked={field.value} onCheckedChange={field.onChange} disabled={field.disabled} />
          </FormControl>
        </FormItem>
      )}
    />
  )
}

// ── 4. Inicio de conversación ───────────────────────────────────────────────

function WelcomeSection({ form }: SectionProps) {
  const welcome = useWatch({ control: form.control, name: 'welcomeMessage' })
  return (
    <SectionCard
      icon={Hand}
      title="Inicio de conversación"
      description="El primer mensaje que recibe un cliente nuevo. Mira cómo se verá en la vista previa."
    >
      <FormField
        control={form.control}
        name="welcomeMessage"
        render={({ field }) => (
          <FormItem>
            <LabelRow aside={<CharCount value={welcome} max={500} />}>
              <FormLabel>
                Mensaje de bienvenida <span className="text-destructive">*</span>
              </FormLabel>
            </LabelRow>
            <FormControl>
              <Textarea
                rows={4}
                maxLength={500}
                placeholder="¡Hola! 👋 Soy Sofía, de Tienda Urbana. ¿Qué estás buscando hoy?"
                {...field}
              />
            </FormControl>
            <FormDescription>Preséntate, menciona tu negocio y termina con una pregunta para iniciar la conversación.</FormDescription>
            <FormMessage />
          </FormItem>
        )}
      />
    </SectionCard>
  )
}

// ── 5. Mensajes ─────────────────────────────────────────────────────────────

function MessagesSection({ form }: SectionProps) {
  const confirmation = useWatch({ control: form.control, name: 'purchaseConfirmationMessage' })
  return (
    <SectionCard icon={MessageSquareHeart} title="Mensajes" description="Textos que el vendedor envía en momentos clave de la venta.">
      <FormField
        control={form.control}
        name="purchaseConfirmationMessage"
        render={({ field }) => (
          <FormItem>
            <LabelRow aside={<CharCount value={confirmation} max={500} />}>
              <FormLabel>Mensaje de confirmación de compra</FormLabel>
            </LabelRow>
            <FormControl>
              <Textarea
                rows={3}
                maxLength={500}
                placeholder="¡Gracias por tu compra! 🎉 Estamos preparando tu pedido y te avisaremos cuando salga."
                {...field}
              />
            </FormControl>
            <FormDescription>Se envía cuando el cliente confirma su pedido. Si lo dejas vacío se usa un mensaje genérico.</FormDescription>
            <FormMessage />
          </FormItem>
        )}
      />
    </SectionCard>
  )
}

// ── 6. Derivación humana ────────────────────────────────────────────────────

function HandoffSection({ form }: SectionProps) {
  const message = useWatch({ control: form.control, name: 'handoffMessage' })
  return (
    <SectionCard
      icon={Headset}
      title="Derivación humana"
      description="Cuándo y cómo el vendedor pasa la conversación a una persona de tu equipo."
    >
      <FormField
        control={form.control}
        name="humanHandoffSituations"
        render={({ field }) => (
          <FormItem>
            <FormLabel>Situaciones que requieren un humano</FormLabel>
            <FormControl>
              <TagInput
                value={field.value}
                onChange={field.onChange}
                disabled={field.disabled}
                maxItems={20}
                maxLength={200}
                itemLabel="situación"
                placeholder="Ej. Reclamos por un pedido entregado"
              />
            </FormControl>
            <FormDescription>
              Si no indicas ninguna, el vendedor deriva cuando el cliente pide hablar con una persona, ante reclamos o
              consultas que no puede resolver.
            </FormDescription>
            <FormMessage />
          </FormItem>
        )}
      />
      <SwitchField
        form={form}
        name="autoPauseOnHandoff"
        label="Pausar al vendedor IA al derivar"
        description="El bot deja de responder en esa conversación hasta que un asesor la retome."
      />
      <FormField
        control={form.control}
        name="handoffMessage"
        render={({ field }) => (
          <FormItem>
            <LabelRow aside={<CharCount value={message} max={300} />}>
              <FormLabel>Mensaje de derivación (opcional)</FormLabel>
            </LabelRow>
            <FormControl>
              <Input maxLength={300} placeholder="Dame un momento, te comunico con un asesor. 🙌" {...field} />
            </FormControl>
            <FormMessage />
          </FormItem>
        )}
      />
    </SectionCard>
  )
}

// ── Vista previa en vivo ────────────────────────────────────────────────────

function LivePreview({ form }: SectionProps) {
  const [time] = useState(nowTime)
  const [name, welcome, length, useEmojis, palette, opening] = useWatch({
    control: form.control,
    name: ['vendorName', 'welcomeMessage', 'responseLength', 'useEmojis', 'emojiPalette', 'useOpeningPunctuation'],
  })

  return (
    <Card className="gap-0 overflow-hidden py-0 lg:sticky lg:top-20">
      <div className="flex items-center justify-between gap-2 px-4 py-3">
        <p className="flex items-center gap-2 text-sm font-semibold">
          <MessagesSquare className="text-primary size-4" aria-hidden /> Vista previa
        </p>
        <Badge variant="outline" className="gap-1.5">
          <span className="bg-success size-1.5 animate-pulse rounded-full" aria-hidden /> En vivo
        </Badge>
      </div>
      <ChatHeader name={name.trim() || 'Tu vendedor'} status="en línea" className="border-t" />
      <ChatSurface className="min-h-64 space-y-2 p-4">
        <ChatNotice>Hoy</ChatNotice>
        <ChatBubble side="out" time={time}>
          Hola
        </ChatBubble>
        <ChatBubble side="in" time={time}>
          {welcome.trim() ? (
            welcome
          ) : (
            <span className="text-muted-foreground italic">Escribe tu mensaje de bienvenida…</span>
          )}
        </ChatBubble>
      </ChatSurface>
      <dl className="grid grid-cols-[auto_1fr] gap-x-3 gap-y-1.5 border-t px-4 py-3 text-xs">
        <dt className="text-muted-foreground">Longitud</dt>
        <dd className="font-medium">{responseLengthLabels[length - 1]}</dd>
        <dt className="text-muted-foreground">Emojis</dt>
        <dd className="font-medium">
          {!useEmojis ? 'No usa' : palette.length > 0 ? <span className="tracking-wider">{palette.join(' ')}</span> : 'Con moderación'}
        </dd>
        <dt className="text-muted-foreground">Signos ¿ ¡</dt>
        <dd className="font-medium">{opening ? 'Sí' : 'No (estilo chat)'}</dd>
      </dl>
    </Card>
  )
}
