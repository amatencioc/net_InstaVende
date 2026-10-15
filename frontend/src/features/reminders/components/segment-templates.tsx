import { useQuery } from '@tanstack/react-query'
import { useBlocker } from '@tanstack/react-router'
import { Lightbulb, Loader2, Lock, RotateCcw, Save, Smartphone } from 'lucide-react'
import { useId, useState } from 'react'
import { Alert } from '@/components/shared/alert'
import { useConfirm } from '@/components/shared/confirm-dialog'
import { QueryError } from '@/components/shared/query-state'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { Switch } from '@/components/ui/switch'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Textarea } from '@/components/ui/textarea'
import { useIsAdmin } from '@/lib/auth/session'
import { formatRelative } from '@/lib/format'
import { cn } from '@/lib/utils'
import {
  reminderTemplateQueries,
  useSaveTemplateSegment,
  type CustomerSegment,
  type ReminderTemplateItem,
  type ReminderTemplateSegment,
} from '../api'
import { customerSegments, reminderOrderTitle, segmentMeta, TEMPLATE_MESSAGE_MAX } from '../constants'
import { templateMessageError } from '../schemas'
import { ChatSurface, MessageBubble } from './message-bubble'
import { SuggestionsDialog } from './suggestions-dialog'

type Drafts = Partial<Record<CustomerSegment, ReminderTemplateItem[]>>

function sameItems(a: ReminderTemplateItem[], b: ReminderTemplateItem[]): boolean {
  return (
    a.length === b.length &&
    a.every((x, i) => {
      const y = b[i]
      return !!y && x.order === y.order && x.message === y.message && x.isActive === y.isActive
    })
  )
}

/** Recordatorios automáticos por segmento (Frío / Tibio / Caliente) con previsualización en vivo. */
export function SegmentTemplates() {
  const query = useQuery(reminderTemplateQueries.segments())
  const [active, setActive] = useState<CustomerSegment>('Frio')
  // Solo se guardan los segmentos editados; el resto se lee del servidor.
  const [drafts, setDrafts] = useState<Drafts>({})
  const confirm = useConfirm()

  const server = new Map((query.data ?? []).map((s) => [s.segment, s]))
  const isDirty = (segment: CustomerSegment) => {
    const draft = drafts[segment]
    const saved = server.get(segment)
    return !!draft && !!saved && !sameItems(draft, saved.reminders)
  }
  const anyDirty = customerSegments.some(isDirty)

  useBlocker({
    shouldBlockFn: async () => {
      if (!anyDirty) return false
      const leave = await confirm({
        title: 'Tienes cambios sin guardar',
        description: 'Si sales ahora perderás los cambios en los recordatorios.',
        confirmLabel: 'Salir sin guardar',
        cancelLabel: 'Seguir editando',
        tone: 'destructive',
      })
      return !leave
    },
    enableBeforeUnload: anyDirty,
  })

  if (query.isError) return <QueryError error={query.error} onRetry={() => void query.refetch()} />
  if (query.isPending) return <TemplatesSkeleton />

  return (
    <Tabs value={active} onValueChange={(v) => setActive(v as CustomerSegment)} className="gap-4">
      <div className="space-y-1">
        <h2 className="text-lg font-semibold">Segmento del cliente</h2>
        <p className="text-muted-foreground text-sm">
          Usamos modelos por segmento (Frío, Tibio, Caliente) con ventanas de 2-3h y una segunda de 22-23h
          cuando aplica. El vendedor IA los envía si el cliente deja de responder.
        </p>
      </div>
      <TabsList aria-label="Segmentos" className="h-auto w-full sm:w-fit">
        {customerSegments.map((segment) => {
          const meta = segmentMeta[segment]
          return (
            <TabsTrigger key={segment} value={segment} className="gap-2 px-4 py-1.5">
              <meta.icon aria-hidden />
              {meta.label}
              {isDirty(segment) && (
                <span
                  className="bg-warning size-2 rounded-full"
                  role="status"
                  aria-label="Cambios sin guardar"
                />
              )}
            </TabsTrigger>
          )
        })}
      </TabsList>

      {customerSegments.map((segment) => {
        const saved = server.get(segment)
        if (!saved) return null
        return (
          <TabsContent key={segment} value={segment}>
            <SegmentEditor
              saved={saved}
              items={drafts[segment] ?? saved.reminders}
              dirty={isDirty(segment)}
              onChange={(items) => setDrafts((d) => ({ ...d, [segment]: items }))}
              onReset={() =>
                setDrafts((d) => {
                  const next = { ...d }
                  delete next[segment]
                  return next
                })
              }
            />
          </TabsContent>
        )
      })}
    </Tabs>
  )
}

interface SegmentEditorProps {
  saved: ReminderTemplateSegment
  items: ReminderTemplateItem[]
  dirty: boolean
  onChange: (items: ReminderTemplateItem[]) => void
  onReset: () => void
}

function SegmentEditor({ saved, items, dirty, onChange, onReset }: SegmentEditorProps) {
  const isAdmin = useIsAdmin()
  const save = useSaveTemplateSegment()
  const meta = segmentMeta[saved.segment]
  const visible = items.filter((i) => i.order <= saved.maxReminders)
  const errors = visible.map((i) => templateMessageError(i.message))
  const invalid = errors.some(Boolean)
  const readOnly = !isAdmin

  const update = (order: number, patch: Partial<ReminderTemplateItem>) =>
    onChange(items.map((i) => (i.order === order ? { ...i, ...patch } : i)))

  const onSave = () =>
    save.mutate(
      {
        segment: saved.segment,
        reminders: visible.map((i) => ({ order: i.order, message: i.message.trim(), isActive: i.isActive })),
      },
      { onSuccess: onReset },
    )

  return (
    <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_340px]">
      <div className="space-y-4">
        <Card className="gap-3 py-5">
          <CardHeader className="flex flex-row items-start gap-3 px-5">
            <span className={cn('flex size-10 shrink-0 items-center justify-center rounded-lg', meta.accent)}>
              <meta.icon className="size-5" aria-hidden />
            </span>
            <div className="space-y-1">
              <CardTitle className="flex flex-wrap items-center gap-2">
                Clientes {meta.label.toLowerCase()}s
                {saved.isDefault && (
                  <Badge variant="secondary" className="font-normal">
                    Valores sugeridos
                  </Badge>
                )}
              </CardTitle>
              <CardDescription>{meta.summary}</CardDescription>
            </div>
          </CardHeader>
          <CardContent className="space-y-3 px-5">
            <p className="text-sm">{meta.description}</p>
            <p className="text-muted-foreground flex items-start gap-2 text-sm">
              <Lightbulb className="text-warning mt-0.5 size-4 shrink-0" aria-hidden />
              {meta.tip}
            </p>
          </CardContent>
        </Card>

        {readOnly && (
          <Alert tone="info" title="Solo lectura">
            Solo un administrador del negocio puede modificar los recordatorios automáticos.
          </Alert>
        )}

        {visible.map((item, index) => (
          <ReminderTemplateCard
            key={item.order}
            segment={saved.segment}
            item={item}
            error={errors[index] ?? null}
            readOnly={readOnly}
            onChange={(patch) => update(item.order, patch)}
          />
        ))}

        <div
          className={cn(
            'bg-background/95 sticky bottom-0 z-10 -mx-1 flex flex-col gap-3 rounded-xl border p-3 shadow-sm backdrop-blur sm:flex-row sm:items-center sm:justify-between',
            !dirty && 'shadow-none',
          )}
        >
          <p className="text-muted-foreground flex items-center gap-2 text-sm" aria-live="polite">
            {dirty ? (
              <>
                <span className="bg-warning size-2 rounded-full" aria-hidden />
                Tienes cambios sin guardar
              </>
            ) : saved.updatedAt ? (
              `Guardado ${formatRelative(saved.updatedAt)}`
            ) : (
              'Aún no guardaste este segmento: se usan los mensajes sugeridos.'
            )}
          </p>
          <div className="flex gap-2">
            {dirty && (
              <Button variant="ghost" onClick={onReset} disabled={save.isPending}>
                <RotateCcw aria-hidden /> Descartar
              </Button>
            )}
            <Button
              onClick={onSave}
              disabled={readOnly || invalid || save.isPending || (!dirty && !saved.isDefault)}
            >
              {save.isPending ? (
                <Loader2 className="animate-spin" aria-hidden />
              ) : readOnly ? (
                <Lock aria-hidden />
              ) : (
                <Save aria-hidden />
              )}
              Guardar {meta.label}
            </Button>
          </div>
        </div>
      </div>

      <PreviewPanel segment={saved.segment} items={visible} />
    </div>
  )
}

function ReminderTemplateCard({
  segment,
  item,
  error,
  readOnly,
  onChange,
}: {
  segment: CustomerSegment
  item: ReminderTemplateItem
  error: string | null
  readOnly: boolean
  onChange: (patch: Partial<ReminderTemplateItem>) => void
}) {
  const id = useId()
  const title = reminderOrderTitle[item.order] ?? `Recordatorio ${item.order}`
  const length = item.message.length
  const remaining = TEMPLATE_MESSAGE_MAX - length

  return (
    <Card className={cn('gap-4 py-5 transition-opacity', !item.isActive && 'opacity-75')}>
      <CardHeader className="flex flex-row items-center justify-between gap-3 px-5">
        <div className="flex min-w-0 items-center gap-3">
          <span
            className={cn(
              'flex size-8 shrink-0 items-center justify-center rounded-full text-sm font-semibold',
              item.isActive ? 'bg-primary text-primary-foreground' : 'bg-muted text-muted-foreground',
            )}
            aria-hidden
          >
            {item.order}
          </span>
          <div className="flex min-w-0 flex-wrap items-center gap-2">
            <CardTitle id={`${id}-title`} className="text-base">
              {title}
            </CardTitle>
            <Badge variant="secondary" className="tabular-nums">
              {item.timeWindow}
            </Badge>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <Label htmlFor={`${id}-active`} className="text-muted-foreground text-sm font-normal">
            {item.isActive ? 'Activo' : 'Inactivo'}
          </Label>
          <Switch
            id={`${id}-active`}
            checked={item.isActive}
            onCheckedChange={(isActive) => onChange({ isActive })}
            disabled={readOnly}
            aria-describedby={`${id}-title`}
          />
        </div>
      </CardHeader>
      <CardContent className="space-y-2 px-5">
        <div className="flex items-center justify-between gap-2">
          <Label htmlFor={`${id}-message`}>Mensaje</Label>
          <SuggestionsDialog
            segment={segment}
            order={item.order}
            current={item.message}
            onPick={(message) => onChange({ message })}
            disabled={readOnly}
          />
        </div>
        <Textarea
          id={`${id}-message`}
          value={item.message}
          onChange={(e) => onChange({ message: e.target.value })}
          maxLength={TEMPLATE_MESSAGE_MAX}
          rows={3}
          readOnly={readOnly}
          aria-invalid={!!error}
          aria-describedby={`${id}-hint`}
          className="min-h-20 resize-y"
        />
        <div id={`${id}-hint`} className="flex items-center justify-between gap-2 text-xs">
          <span className={error ? 'text-destructive' : 'text-muted-foreground'}>
            {error ?? `Se envía ${item.timeWindow} después del último mensaje del cliente si no responde.`}
          </span>
          <span
            className={cn(
              'shrink-0 tabular-nums',
              remaining <= 0
                ? 'text-destructive'
                : remaining <= 20
                  ? 'text-warning'
                  : 'text-muted-foreground',
            )}
          >
            {length}/{TEMPLATE_MESSAGE_MAX}
          </span>
        </div>
      </CardContent>
    </Card>
  )
}

function PreviewPanel({ segment, items }: { segment: CustomerSegment; items: ReminderTemplateItem[] }) {
  const active = items.filter((i) => i.isActive)
  const meta = segmentMeta[segment]
  return (
    <aside aria-label="Previsualización" className="lg:sticky lg:top-20 lg:self-start">
      <Card className="gap-0 overflow-hidden py-0">
        <div className="flex items-center gap-2 border-b px-4 py-3">
          <Smartphone className="text-muted-foreground size-4" aria-hidden />
          <h3 className="text-sm font-semibold">Previsualización</h3>
          <Badge variant="outline" className="ml-auto font-normal">
            {meta.label}
          </Badge>
        </div>
        <ChatSurface className="min-h-80 rounded-none">
          <p className="bg-background/80 text-muted-foreground mx-auto rounded-md px-2 py-0.5 text-[11px] shadow-xs">
            Hoy
          </p>
          <div className="flex justify-start">
            <p className="bg-card max-w-[85%] rounded-lg rounded-tl-none px-3 py-2 text-sm shadow-xs">
              Hola, quería consultar por un producto 🙂
            </p>
          </div>
          {active.length === 0 ? (
            <p className="text-muted-foreground mt-auto text-center text-sm">
              No hay recordatorios activos en este segmento: no se enviará ningún mensaje.
            </p>
          ) : (
            active.map((item) => (
              <div key={item.order} className="space-y-1.5">
                <p className="text-muted-foreground text-center text-[11px] font-medium">
                  {reminderOrderTitle[item.order] ?? `Recordatorio ${item.order}`} ({item.timeWindow})
                </p>
                <MessageBubble message={item.message} time="18:20" />
              </div>
            ))
          )}
        </ChatSurface>
      </Card>
    </aside>
  )
}

function TemplatesSkeleton() {
  return (
    <div className="space-y-4" aria-busy="true" aria-label="Cargando recordatorios automáticos">
      <Skeleton className="h-6 w-48" />
      <Skeleton className="h-9 w-80 max-w-full" />
      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_340px]">
        <div className="space-y-4">
          <Skeleton className="h-36 w-full rounded-xl" />
          <Skeleton className="h-52 w-full rounded-xl" />
          <Skeleton className="h-52 w-full rounded-xl" />
        </div>
        <Skeleton className="h-96 w-full rounded-xl" />
      </div>
    </div>
  )
}
