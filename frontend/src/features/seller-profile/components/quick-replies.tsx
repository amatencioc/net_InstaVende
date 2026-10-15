import { zodResolver } from '@hookform/resolvers/zod'
import { useQuery } from '@tanstack/react-query'
import { Loader2, MessageSquareReply, Pencil, Plus, Trash2, Zap } from 'lucide-react'
import { useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { useConfirm } from '@/components/shared/confirm-dialog'
import { EmptyState } from '@/components/shared/empty-state'
import { QueryError } from '@/components/shared/query-state'
import { Badge } from '@/components/ui/badge'
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
import { Skeleton } from '@/components/ui/skeleton'
import { Switch } from '@/components/ui/switch'
import { Textarea } from '@/components/ui/textarea'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { ApiError } from '@/lib/api/client'
import { cn } from '@/lib/utils'
import {
  quickRepliesQuery,
  useDeleteQuickReply,
  useSaveQuickReply,
  useToggleQuickReply,
  type QuickReply,
} from '../api'
import { quickReplySchema, toQuickReplyForm, toQuickReplyRequest, type QuickReplyFormValues } from '../schemas'
import { AffixNumberInput, CharCount, LabelRow, SectionCard } from './form-bits'
import { TagInput } from './tag-input'

/** Tarjeta CRUD de respuestas fijas (BotIntent). Se guardan al instante, independientes del formulario de reglas. */
export function QuickRepliesCard({ canEdit }: { canEdit: boolean }) {
  const query = useQuery(quickRepliesQuery)
  const [editing, setEditing] = useState<QuickReply | 'new' | null>(null)

  const addButton = canEdit && (
    <Button type="button" size="sm" onClick={() => setEditing('new')}>
      <Plus /> Nueva respuesta
    </Button>
  )

  return (
    <SectionCard
      id="respuestas-fijas"
      icon={Zap}
      title="Respuestas fijas"
      description="Respuestas exactas para frases concretas. Se envían al instante, sin pasar por la IA, y se guardan en cuanto las creas."
      action={query.data && query.data.length > 0 ? addButton : undefined}
    >
      {query.isPending ? (
        <div className="space-y-3" aria-busy="true">
          {[0, 1, 2].map((i) => (
            <Skeleton key={i} className="h-20 rounded-lg" />
          ))}
        </div>
      ) : query.isError ? (
        <QueryError error={query.error} onRetry={() => void query.refetch()} />
      ) : query.data.length === 0 ? (
        <EmptyState
          icon={MessageSquareReply}
          title="Aún no tienes respuestas fijas"
          description='Úsalas para preguntas que siempre se responden igual, como "¿Hacen envíos?" o "¿Cuál es su dirección?".'
          action={addButton}
        />
      ) : (
        <ul className="divide-y rounded-lg border">
          {query.data.map((reply) => (
            <QuickReplyRow key={reply.id} reply={reply} canEdit={canEdit} onEdit={() => setEditing(reply)} />
          ))}
        </ul>
      )}

      <QuickReplyDialog
        // Se remonta al cambiar de elemento para reiniciar el formulario.
        key={editing === 'new' ? 'new' : (editing?.id ?? 'closed')}
        reply={editing === 'new' ? undefined : (editing ?? undefined)}
        open={editing !== null}
        onClose={() => setEditing(null)}
      />
    </SectionCard>
  )
}

function QuickReplyRow({ reply, canEdit, onEdit }: { reply: QuickReply; canEdit: boolean; onEdit: () => void }) {
  const confirm = useConfirm()
  const toggle = useToggleQuickReply()
  const remove = useDeleteQuickReply()
  const visible = reply.triggerPhrases.slice(0, 4)
  const hidden = reply.triggerPhrases.length - visible.length

  async function onDelete() {
    const ok = await confirm({
      title: 'Eliminar respuesta fija',
      description: `Se eliminará "${reply.name}". El vendedor dejará de usar esta respuesta.`,
      confirmLabel: 'Eliminar',
      tone: 'destructive',
    })
    if (ok) remove.mutate(reply.id)
  }

  return (
    <li className={cn('flex flex-col gap-3 p-4 sm:flex-row sm:items-start', !reply.isActive && 'bg-muted/40')}>
      <div className="min-w-0 flex-1 space-y-2">
        <div className="flex flex-wrap items-center gap-2">
          <p className={cn('font-medium', !reply.isActive && 'text-muted-foreground')}>{reply.name}</p>
          {reply.priority > 0 && (
            <Badge variant="outline" className="tabular-nums">
              Prioridad {reply.priority}
            </Badge>
          )}
          {!reply.isActive && <Badge variant="secondary">Inactiva</Badge>}
        </div>
        <ul className="flex flex-wrap gap-1.5" aria-label="Frases disparadoras">
          {visible.map((p) => (
            <li key={p} className="bg-secondary text-secondary-foreground rounded-full px-2.5 py-0.5 text-xs">
              {p}
            </li>
          ))}
          {hidden > 0 && <li className="text-muted-foreground px-1 py-0.5 text-xs">+{hidden} más</li>}
        </ul>
        <p className="text-muted-foreground line-clamp-2 text-sm whitespace-pre-line">{reply.response}</p>
      </div>
      {canEdit && (
        <div className="flex items-center gap-1 sm:pt-0.5">
          <Tooltip>
            <TooltipTrigger asChild>
              <span className="mr-2 inline-flex">
                <Switch
                  checked={reply.isActive}
                  disabled={toggle.isPending}
                  onCheckedChange={() => toggle.mutate(reply)}
                  aria-label={reply.isActive ? `Desactivar "${reply.name}"` : `Activar "${reply.name}"`}
                />
              </span>
            </TooltipTrigger>
            <TooltipContent>{reply.isActive ? 'Activa' : 'Inactiva'}</TooltipContent>
          </Tooltip>
          <Button type="button" variant="ghost" size="icon-sm" onClick={onEdit} aria-label={`Editar "${reply.name}"`}>
            <Pencil />
          </Button>
          <Button
            type="button"
            variant="ghost"
            size="icon-sm"
            onClick={() => void onDelete()}
            disabled={remove.isPending}
            className="text-muted-foreground hover:text-destructive"
            aria-label={`Eliminar "${reply.name}"`}
          >
            <Trash2 />
          </Button>
        </div>
      )}
    </li>
  )
}

function QuickReplyDialog({ reply, open, onClose }: { reply?: QuickReply; open: boolean; onClose: () => void }) {
  const save = useSaveQuickReply()
  const form = useForm<QuickReplyFormValues>({
    resolver: zodResolver(quickReplySchema),
    defaultValues: toQuickReplyForm(reply),
  })
  const response = useWatch({ control: form.control, name: 'response' })
  const isEdit = reply != null

  const onSubmit = form.handleSubmit((values) =>
    save.mutate(
      { id: reply?.id, body: toQuickReplyRequest(values) },
      {
        onSuccess: onClose,
        onError: (error) => {
          if (!(error instanceof ApiError)) return
          for (const [field, message] of Object.entries(error.fieldErrors))
            if (field in values) form.setError(field as keyof QuickReplyFormValues, { message })
        },
      },
    ),
  )

  return (
    <Dialog open={open} onOpenChange={(o) => !o && !save.isPending && onClose()}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{isEdit ? 'Editar respuesta fija' : 'Nueva respuesta fija'}</DialogTitle>
          <DialogDescription>
            Si el mensaje del cliente contiene alguna de las frases, el vendedor responde exactamente este texto.
          </DialogDescription>
        </DialogHeader>
        <Form {...form}>
          <form id="quick-reply-form" onSubmit={onSubmit} noValidate className="space-y-4">
            <FormField
              control={form.control}
              name="name"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Nombre</FormLabel>
                  <FormControl>
                    <Input placeholder="Ej. Horario de la tienda" maxLength={200} autoFocus {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="triggerPhrases"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Frases disparadoras</FormLabel>
                  <FormControl>
                    <TagInput
                      value={field.value}
                      onChange={field.onChange}
                      minLength={3}
                      maxLength={100}
                      maxItems={20}
                      itemLabel="frase"
                      placeholder="Ej. a qué hora abren"
                    />
                  </FormControl>
                  <FormDescription>Mínimo 3 caracteres. No distingue mayúsculas ni tildes.</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="response"
              render={({ field }) => (
                <FormItem>
                  <LabelRow aside={<CharCount value={response} max={1000} />}>
                    <FormLabel>Respuesta</FormLabel>
                  </LabelRow>
                  <FormControl>
                    <Textarea
                      rows={4}
                      maxLength={1000}
                      placeholder="Atendemos de lunes a sábado de 9:00 a 20:00. ¡Te esperamos! 😊"
                      {...field}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <div className="grid gap-4 sm:grid-cols-2">
              <FormField
                control={form.control}
                name="priority"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Prioridad</FormLabel>
                    <FormControl>
                      <AffixNumberInput
                        value={field.value}
                        onChange={field.onChange}
                        onBlur={field.onBlur}
                        name={field.name}
                        ref={field.ref}
                        min={0}
                        max={100}
                        step={1}
                        inputMode="numeric"
                      />
                    </FormControl>
                    <FormDescription>Si varias coinciden, gana la de mayor prioridad.</FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="isActive"
                render={({ field }) => (
                  <FormItem className="flex h-fit items-center justify-between gap-3 rounded-lg border p-3 sm:mt-5.5">
                    <FormLabel>Activa</FormLabel>
                    <FormControl>
                      <Switch checked={field.value} onCheckedChange={field.onChange} />
                    </FormControl>
                  </FormItem>
                )}
              />
            </div>
          </form>
        </Form>
        <DialogFooter>
          <Button type="button" variant="outline" onClick={onClose} disabled={save.isPending}>
            Cancelar
          </Button>
          <Button type="submit" form="quick-reply-form" disabled={save.isPending}>
            {save.isPending && <Loader2 className="animate-spin" />}
            {isEdit ? 'Guardar cambios' : 'Crear respuesta'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
