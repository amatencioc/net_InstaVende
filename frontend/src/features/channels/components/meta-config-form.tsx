import { zodResolver } from '@hookform/resolvers/zod'
import { CheckCircle2, Eye, EyeOff, Loader2, PlugZap, Save } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useForm, type Control } from 'react-hook-form'
import { toast } from 'sonner'
import { z } from 'zod'
import { useSaveMetaConfig, useTestChannel, type ChannelMetaConfigRequest, type ChannelSummary } from '../api'
import { metaChannelInfo } from '../channel-info'
import { ChannelStatusBadge } from './channel-status-badge'
import { FieldHelp } from './field-help'
import { Alert } from '@/components/shared/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'
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
import { Switch } from '@/components/ui/switch'
import { ApiError } from '@/lib/api/client'

const digits = (message: string) =>
  z.string().trim().max(32, 'El identificador es demasiado largo.').regex(/^\d*$/, message)
const secret = (label: string, max: number) =>
  z
    .string()
    .trim()
    .max(max, `${label} es demasiado largo.`)
    .regex(/^\S*$/, `${label} no puede contener espacios.`)

const baseSchema = z.object({
  accountId: digits('Solo debe contener números.').min(1, 'Este identificador es obligatorio.'),
  wabaId: digits('Solo debe contener números.'),
  accessToken: secret('El token', 2048),
  appSecret: secret('La clave secreta', 256),
  isActive: z.boolean(),
})
type FormValues = z.infer<typeof baseSchema>

/** El switch de WhatsApp representa la Cloud API (no la sesión QR). */
function isMetaActive(summary: ChannelSummary): boolean {
  return summary.isActive && (summary.channel !== 'WhatsApp' || summary.whatsAppMode === 'CloudApi')
}

function valuesFrom(summary: ChannelSummary): FormValues {
  const info = metaChannelInfo[summary.channel]
  return {
    accountId: summary[info.accountField] ?? '',
    wabaId: summary.wabaId ?? '',
    accessToken: '',
    appSecret: '',
    isActive: isMetaActive(summary),
  }
}

const serverFieldMap: Record<string, keyof FormValues> = {
  phoneNumberId: 'accountId',
  pageId: 'accountId',
  instagramAccountId: 'accountId',
  wabaId: 'wabaId',
  accessToken: 'accessToken',
  appSecret: 'appSecret',
}

/** Formulario de credenciales de Meta de un canal (WhatsApp Cloud API, Messenger o Instagram). */
export function MetaConfigForm({ summary, isAdmin }: { summary: ChannelSummary; isAdmin: boolean }) {
  const channel = summary.channel
  const info = metaChannelInfo[channel]
  const save = useSaveMetaConfig(channel)
  const test = useTestChannel(channel)
  const qrActive = channel === 'WhatsApp' && summary.isActive && summary.whatsAppMode === 'Qr'

  const schema = useMemo(
    () =>
      baseSchema.superRefine((v, ctx) => {
        if (!v.isActive) return
        if (!v.accessToken && !summary.accessToken.configured)
          ctx.addIssue({
            code: 'custom',
            path: ['accessToken'],
            message: 'Ingresa el token de acceso para activar el canal.',
          })
        if (!v.appSecret && !summary.appSecret.configured)
          ctx.addIssue({
            code: 'custom',
            path: ['appSecret'],
            message: 'Ingresa la clave secreta para activar el canal.',
          })
      }),
    [summary.accessToken.configured, summary.appSecret.configured],
  )
  const values = useMemo(() => valuesFrom(summary), [summary])
  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    values,
    resetOptions: { keepDirtyValues: true },
  })
  const dirty = form.formState.isDirty

  const runTest = () =>
    test.mutate(undefined, {
      onSuccess: (r) =>
        r.ok ? toast.success('Conexión verificada') : toast.error('La prueba de conexión falló'),
    })

  const onSubmit = form.handleSubmit((v) => {
    test.reset()
    const body: ChannelMetaConfigRequest = {
      phoneNumberId: channel === 'WhatsApp' ? v.accountId : null,
      wabaId: channel === 'WhatsApp' ? v.wabaId || null : null,
      pageId: channel === 'FacebookMessenger' ? v.accountId : null,
      instagramAccountId: channel === 'Instagram' ? v.accountId : null,
      accessToken: v.accessToken || null,
      appSecret: v.appSecret || null,
      isActive: v.isActive,
    }
    save.mutate(body, {
      onSuccess: (result) => {
        form.reset(valuesFrom(result))
        toast.success(
          result.isActive ? 'Configuración guardada y canal activado.' : 'Configuración guardada.',
        )
        if (result.isActive) runTest()
      },
      onError: (error) => {
        if (!(error instanceof ApiError)) return
        for (const [field, message] of Object.entries(error.fieldErrors)) {
          const target = serverFieldMap[field]
          if (target) form.setError(target, { message })
        }
      },
    })
  })

  const showSaveError = save.error && !(save.error instanceof ApiError && save.error.status === 400)

  return (
    <Card>
      <CardHeader className="flex flex-row flex-wrap items-start justify-between gap-3">
        <div className="space-y-1.5">
          <CardTitle className="text-lg">{info.title}</CardTitle>
          <CardDescription>{info.description}</CardDescription>
        </div>
        <ChannelStatusBadge
          status={channel === 'WhatsApp' && !isMetaActive(summary) ? 'Disconnected' : summary.status}
        />
      </CardHeader>
      <Form {...form}>
        <form onSubmit={onSubmit} noValidate>
          <CardContent className="space-y-5">
            {!isAdmin && (
              <Alert tone="info">Solo un administrador del negocio puede modificar esta configuración.</Alert>
            )}
            {qrActive && (
              <Alert tone="warning" title="Tu WhatsApp está vinculado con código QR">
                Si activas la API oficial se cerrará la sesión QR y los mensajes pasarán a gestionarse por la
                Cloud API.
              </Alert>
            )}
            {summary.status === 'NeedsAttention' && summary.statusDetail && isMetaActive(summary) && (
              <Alert tone="warning">{summary.statusDetail}</Alert>
            )}
            {showSaveError && <Alert tone="destructive">{save.error.message}</Alert>}

            <fieldset disabled={!isAdmin || save.isPending} className="space-y-5">
              <legend className="sr-only">Credenciales de {info.title}</legend>
              <div className="grid gap-5 sm:grid-cols-2">
                <FormField
                  control={form.control}
                  name="accountId"
                  render={({ field }) => (
                    <FormItem className={channel === 'WhatsApp' ? undefined : 'sm:col-span-2'}>
                      <div className="flex items-center gap-1.5">
                        <FormLabel>{info.accountLabel}</FormLabel>
                        <FieldHelp label={info.accountLabel}>{info.accountHelp}</FieldHelp>
                      </div>
                      <FormControl>
                        <Input
                          inputMode="numeric"
                          autoComplete="off"
                          placeholder={info.accountPlaceholder}
                          {...field}
                        />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                {channel === 'WhatsApp' && (
                  <FormField
                    control={form.control}
                    name="wabaId"
                    render={({ field }) => (
                      <FormItem>
                        <div className="flex items-center gap-1.5">
                          <FormLabel>Identificador de la cuenta de WhatsApp Business</FormLabel>
                          <FieldHelp label="Cuenta de WhatsApp Business">
                            Aparece junto al identificador del número en WhatsApp › Configuración de la API.
                            Es opcional.
                          </FieldHelp>
                        </div>
                        <FormControl>
                          <Input inputMode="numeric" autoComplete="off" placeholder="Opcional" {...field} />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                )}
              </div>

              <SecretField
                control={form.control}
                name="accessToken"
                label={info.tokenLabel}
                help={info.tokenHelp}
                hint={summary.accessToken}
                placeholder="Pega aquí el token de acceso"
              />
              <SecretField
                control={form.control}
                name="appSecret"
                label="Clave secreta de la app (App Secret)"
                help={info.appSecretHelp}
                hint={summary.appSecret}
                placeholder="Pega aquí la clave secreta"
              />

              <FormField
                control={form.control}
                name="isActive"
                render={({ field }) => (
                  <FormItem className="bg-muted/40 flex flex-row items-center justify-between gap-4 rounded-lg border p-4">
                    <div className="space-y-0.5">
                      <FormLabel>Canal activo</FormLabel>
                      <FormDescription>
                        Tu vendedor responderá los mensajes que lleguen por este canal.
                      </FormDescription>
                    </div>
                    <FormControl>
                      <Switch checked={field.value} onCheckedChange={field.onChange} />
                    </FormControl>
                  </FormItem>
                )}
              />
            </fieldset>

            {test.data && (
              <Alert
                tone={test.data.ok ? 'success' : 'destructive'}
                title={test.data.ok ? 'Conexión verificada' : 'No se pudo verificar'}
              >
                {test.data.message}
              </Alert>
            )}
          </CardContent>
          {isAdmin && (
            <CardFooter className="mt-6 flex flex-wrap items-center gap-2 border-t pt-6">
              <Button type="submit" disabled={!dirty || save.isPending}>
                {save.isPending ? <Loader2 className="animate-spin" /> : <Save />}
                Guardar cambios
              </Button>
              <Button
                type="button"
                variant="outline"
                onClick={runTest}
                disabled={!summary.configured || dirty || test.isPending || save.isPending}
              >
                {test.isPending ? <Loader2 className="animate-spin" /> : <PlugZap />}
                Probar conexión
              </Button>
              {dirty && (
                <p className="text-muted-foreground text-xs">
                  Guarda los cambios antes de probar la conexión.
                </p>
              )}
            </CardFooter>
          )}
        </form>
      </Form>
    </Card>
  )
}

interface SecretFieldProps {
  control: Control<FormValues>
  name: 'accessToken' | 'appSecret'
  label: string
  help: string
  hint: ChannelSummary['accessToken']
  placeholder: string
}

/** Campo de secreto: nunca muestra el valor guardado, solo si existe y sus últimos 4 caracteres. */
function SecretField({ control, name, label, help, hint, placeholder }: SecretFieldProps) {
  const [visible, setVisible] = useState(false)
  return (
    <FormField
      control={control}
      name={name}
      render={({ field }) => (
        <FormItem>
          <div className="flex items-center gap-1.5">
            <FormLabel>{label}</FormLabel>
            <FieldHelp label={label}>{help}</FieldHelp>
          </div>
          <div className="relative">
            <FormControl>
              <Input
                type={visible ? 'text' : 'password'}
                autoComplete="off"
                spellCheck={false}
                className="pr-10 font-mono"
                placeholder={hint.configured ? 'Deja vacío para conservar el actual' : placeholder}
                {...field}
              />
            </FormControl>
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              className="absolute top-0.5 right-0.5"
              onClick={() => setVisible((v) => !v)}
              aria-label={visible ? `Ocultar ${label.toLowerCase()}` : `Mostrar ${label.toLowerCase()}`}
              aria-pressed={visible}
            >
              {visible ? <EyeOff /> : <Eye />}
            </Button>
          </div>
          <FormDescription className="flex items-center gap-1.5">
            {hint.configured ? (
              <>
                <CheckCircle2 className="text-success size-3.5" aria-hidden />
                {hint.last4 ? `Configurado ••••${hint.last4}` : 'Configurado'}
              </>
            ) : (
              'Sin configurar'
            )}
          </FormDescription>
          <FormMessage />
        </FormItem>
      )}
    />
  )
}
