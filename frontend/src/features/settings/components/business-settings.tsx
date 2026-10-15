import { zodResolver } from '@hookform/resolvers/zod'
import { useQuery } from '@tanstack/react-query'
import { Building2, Loader2 } from 'lucide-react'
import type { ComponentProps } from 'react'
import { useForm, type Control } from 'react-hook-form'
import { Alert } from '@/components/shared/alert'
import { QueryError } from '@/components/shared/query-state'
import { Button } from '@/components/ui/button'
import { Form, FormControl, FormDescription, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Textarea } from '@/components/ui/textarea'
import { ApiError } from '@/lib/api/client'
import { businessSettingsQuery, useDeleteLogo, useUpdateBusiness, useUploadLogo, type SettingsBusiness } from '../api'
import { businessSchema, nullIfEmpty, type BusinessFormValues } from '../schemas'
import { ImageDropzone } from './image-dropzone'
import { SettingsCard, SettingsCardSkeleton } from './settings-card'

const NO_SECTOR = '__none__'

export function BusinessSettings() {
  const business = useQuery(businessSettingsQuery)

  if (business.isPending)
    return (
      <div className="space-y-6">
        <SettingsCardSkeleton rows={1} />
        <SettingsCardSkeleton rows={3} />
        <SettingsCardSkeleton rows={2} />
      </div>
    )
  if (business.isError) return <QueryError error={business.error} onRetry={() => void business.refetch()} />

  return (
    <div className="space-y-6">
      <LogoCard business={business.data} />
      <BusinessForm business={business.data} />
    </div>
  )
}

function LogoCard({ business }: { business: SettingsBusiness }) {
  const upload = useUploadLogo()
  const remove = useDeleteLogo()
  return (
    <SettingsCard title="Logo" description="Identifica a tu negocio en el panel y en las invitaciones a tu equipo.">
      <ImageDropzone
        label="logo del negocio"
        shape="square"
        imageUrl={business.logoUrl}
        fallback={<Building2 className="text-muted-foreground size-8" aria-hidden />}
        uploading={upload.isPending}
        removing={remove.isPending}
        onUpload={(file) => upload.mutateAsync(file).catch(() => undefined)}
        onRemove={() => remove.mutate()}
      />
    </SettingsCard>
  )
}

function toFormValues(b: SettingsBusiness): BusinessFormValues {
  return {
    name: b.name,
    description: b.description ?? '',
    sector: b.sector ?? '',
    country: b.country ?? '',
    currency: b.currency ?? '',
    phone: b.phone ?? '',
    whatsAppNumber: b.whatsAppNumber ?? '',
    email: b.email ?? '',
    websiteUrl: b.websiteUrl ?? '',
    socialMedia: b.socialMedia ?? '',
  }
}

function BusinessForm({ business }: { business: SettingsBusiness }) {
  const update = useUpdateBusiness()
  const form = useForm<BusinessFormValues>({
    resolver: zodResolver(businessSchema),
    values: toFormValues(business),
    resetOptions: { keepDirtyValues: true },
  })

  // Un rubro heredado que no está en la lista sigue siendo seleccionable
  const sectors =
    business.sector && !business.sectorOptions.includes(business.sector)
      ? [business.sector, ...business.sectorOptions]
      : business.sectorOptions

  const onSubmit = form.handleSubmit((v) =>
    update.mutate(
      {
        name: v.name.trim(),
        description: nullIfEmpty(v.description),
        sector: nullIfEmpty(v.sector),
        country: v.country,
        currency: v.currency,
        phone: nullIfEmpty(v.phone),
        whatsAppNumber: nullIfEmpty(v.whatsAppNumber),
        email: nullIfEmpty(v.email),
        websiteUrl: nullIfEmpty(v.websiteUrl),
        socialMedia: nullIfEmpty(v.socialMedia),
      },
      {
        onSuccess: (saved) => form.reset(toFormValues(saved)),
        onError: (error) => {
          if (error instanceof ApiError)
            for (const [field, message] of Object.entries(error.fieldErrors))
              form.setError(field as keyof BusinessFormValues, { message })
        },
      },
    ),
  )

  const hasFieldErrors = update.error instanceof ApiError && Object.keys(update.error.fieldErrors).length > 0
  const dirty = form.formState.isDirty

  return (
    <Form {...form}>
      <form onSubmit={onSubmit} noValidate className="space-y-6">
        {update.error && !hasFieldErrors && <Alert tone="destructive">{update.error.message}</Alert>}

        <SettingsCard title="Datos del negocio" description="Tu vendedor IA usa esta información para presentarse.">
          <div className="space-y-4">
            <div className="grid gap-4 sm:grid-cols-2">
              <TextField control={form.control} name="name" label="Nombre del negocio" autoComplete="organization" />
              <FormField
                control={form.control}
                name="sector"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Sector o rubro</FormLabel>
                    <Select
                      value={field.value === '' ? NO_SECTOR : field.value}
                      onValueChange={(v) => field.onChange(v === NO_SECTOR ? '' : v)}
                    >
                      <FormControl>
                        <SelectTrigger className="w-full">
                          <SelectValue placeholder="Selecciona un rubro" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        <SelectItem value={NO_SECTOR}>Sin especificar</SelectItem>
                        {sectors.map((s) => (
                          <SelectItem key={s} value={s}>
                            {s}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>
            <FormField
              control={form.control}
              name="description"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Descripción</FormLabel>
                  <FormControl>
                    <Textarea rows={3} placeholder="Qué vendes, a quién y qué te hace diferente." {...field} />
                  </FormControl>
                  <FormDescription>{field.value.length}/1000 caracteres</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />
          </div>
        </SettingsCard>

        <SettingsCard
          title="País y moneda"
          description="Se usan para mostrar los importes, calcular el horario del negocio y personalizar al vendedor IA."
        >
          <div className="grid gap-4 sm:grid-cols-2">
            <OptionField control={form.control} name="country" label="País" placeholder="Selecciona el país" options={business.countryOptions} />
            <OptionField
              control={form.control}
              name="currency"
              label="Moneda"
              placeholder="Selecciona la moneda"
              options={business.currencyOptions}
            />
          </div>
        </SettingsCard>

        <SettingsCard title="Contacto y redes" description="Datos que el vendedor IA puede compartir con tus clientes.">
          <div className="space-y-4">
            <div className="grid gap-4 sm:grid-cols-2">
              <TextField control={form.control} name="phone" label="Teléfono" type="tel" autoComplete="tel" placeholder="+51 1 555 1234" />
              <TextField
                control={form.control}
                name="whatsAppNumber"
                label="WhatsApp de contacto"
                type="tel"
                placeholder="+51 999 888 777"
                description="Para derivar clientes a una persona."
              />
              <TextField control={form.control} name="email" label="Email de contacto" type="email" autoComplete="email" placeholder="ventas@tunegocio.com" />
              <TextField control={form.control} name="websiteUrl" label="Sitio web" type="url" autoComplete="url" placeholder="https://tunegocio.com" />
            </div>
            <FormField
              control={form.control}
              name="socialMedia"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Redes sociales</FormLabel>
                  <FormControl>
                    <Textarea rows={2} placeholder="Instagram: @tunegocio · Facebook: /tunegocio · TikTok: @tunegocio" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          </div>
        </SettingsCard>

        <div className="bg-background/90 sticky bottom-0 z-10 -mx-1 flex flex-col gap-3 rounded-xl border p-3 shadow-sm backdrop-blur sm:flex-row sm:items-center sm:justify-between">
          <p className="text-muted-foreground text-sm" aria-live="polite">
            {dirty ? 'Tienes cambios sin guardar.' : 'Todo está guardado.'}
          </p>
          <div className="flex gap-2">
            <Button type="button" variant="ghost" disabled={!dirty || update.isPending} onClick={() => form.reset()}>
              Descartar
            </Button>
            <Button type="submit" disabled={!dirty || update.isPending}>
              {update.isPending && <Loader2 className="animate-spin" />}
              Guardar cambios
            </Button>
          </div>
        </div>
      </form>
    </Form>
  )
}

type TextFieldName = 'name' | 'phone' | 'whatsAppNumber' | 'email' | 'websiteUrl'

function TextField({
  control,
  name,
  label,
  description,
  ...props
}: {
  control: Control<BusinessFormValues>
  name: TextFieldName
  label: string
  description?: string
} & Omit<ComponentProps<typeof Input>, 'name'>) {
  return (
    <FormField
      control={control}
      name={name}
      render={({ field }) => (
        <FormItem>
          <FormLabel>{label}</FormLabel>
          <FormControl>
            <Input {...props} {...field} />
          </FormControl>
          {description && <FormDescription>{description}</FormDescription>}
          <FormMessage />
        </FormItem>
      )}
    />
  )
}

function OptionField({
  control,
  name,
  label,
  placeholder,
  options,
}: {
  control: Control<BusinessFormValues>
  name: 'country' | 'currency'
  label: string
  placeholder: string
  options: string[]
}) {
  return (
    <FormField
      control={control}
      name={name}
      render={({ field }) => (
        <FormItem>
          <FormLabel>{label}</FormLabel>
          <Select value={field.value || undefined} onValueChange={field.onChange}>
            <FormControl>
              <SelectTrigger className="w-full" onBlur={field.onBlur}>
                <SelectValue placeholder={placeholder} />
              </SelectTrigger>
            </FormControl>
            <SelectContent>
              {options.map((o) => (
                <SelectItem key={o} value={o}>
                  {o}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <FormMessage />
        </FormItem>
      )}
    />
  )
}
