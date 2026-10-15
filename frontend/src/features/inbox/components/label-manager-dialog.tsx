import { zodResolver } from '@hookform/resolvers/zod'
import { useQuery } from '@tanstack/react-query'
import { Check, Loader2, Pencil, Plus, Tags, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { labelsQuery, useDeleteLabel, useSaveLabel, type ConversationLabel } from '../api'
import { labelColors } from '../meta'
import { LabelDot } from './bits'
import { Alert } from '@/components/shared/alert'
import { useConfirm } from '@/components/shared/confirm-dialog'
import { QueryError } from '@/components/shared/query-state'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { ApiError } from '@/lib/api/client'
import { cn } from '@/lib/utils'

const labelSchema = z.object({
  name: z.string().trim().min(1, 'El nombre es obligatorio.').max(100, 'Máximo 100 caracteres.'),
  color: z.string().regex(/^#[0-9a-fA-F]{6}$/, 'Elige un color.'),
})
type LabelValues = z.infer<typeof labelSchema>

/** Alta, edición y baja de etiquetas de conversación. */
export function LabelManagerDialog({
  open,
  onOpenChange,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
}) {
  const labels = useQuery({ ...labelsQuery, enabled: open })
  const [editing, setEditing] = useState<ConversationLabel | 'new' | null>(null)
  const del = useDeleteLabel()
  const confirm = useConfirm()

  const remove = async (label: ConversationLabel) => {
    const ok = await confirm({
      title: `Eliminar «${label.name}»`,
      description:
        label.conversationCount > 0
          ? `Se quitará de ${label.conversationCount} conversación${label.conversationCount === 1 ? '' : 'es'}. Esta acción no se puede deshacer.`
          : 'Esta acción no se puede deshacer.',
      confirmLabel: 'Eliminar',
      tone: 'destructive',
    })
    if (ok) del.mutate(label.id)
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(v) => {
        onOpenChange(v)
        if (!v) setEditing(null)
      }}
    >
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Etiquetas</DialogTitle>
          <DialogDescription>Organiza tus conversaciones con etiquetas de color.</DialogDescription>
        </DialogHeader>

        {labels.isPending ? (
          <div className="space-y-2">
            {Array.from({ length: 3 }, (_, i) => (
              <Skeleton key={i} className="h-10 w-full" />
            ))}
          </div>
        ) : labels.isError ? (
          <QueryError error={labels.error} onRetry={() => void labels.refetch()} />
        ) : (
          <div className="space-y-3">
            {labels.data.length === 0 && editing === null && (
              <div className="text-muted-foreground flex flex-col items-center gap-2 rounded-lg border border-dashed px-4 py-8 text-center text-sm">
                <Tags className="size-5" aria-hidden />
                Aún no tienes etiquetas. Crea la primera para clasificar tus chats (p. ej. «Mayorista» o
                «Reclamo»).
              </div>
            )}
            {labels.data.length > 0 && (
              <ul className="max-h-72 divide-y overflow-y-auto rounded-lg border">
                {labels.data.map((label) =>
                  editing !== 'new' && editing?.id === label.id ? (
                    <li key={label.id} className="p-3">
                      <LabelForm label={label} onDone={() => setEditing(null)} />
                    </li>
                  ) : (
                    <li key={label.id} className="flex items-center gap-3 px-3 py-2">
                      <LabelDot color={label.color} className="size-3" />
                      <span className="min-w-0 flex-1 truncate text-sm font-medium">{label.name}</span>
                      <span className="text-muted-foreground text-xs tabular-nums">
                        {label.conversationCount}
                      </span>
                      <Button
                        variant="ghost"
                        size="icon-sm"
                        aria-label={`Editar ${label.name}`}
                        onClick={() => setEditing(label)}
                      >
                        <Pencil />
                      </Button>
                      <Button
                        variant="ghost"
                        size="icon-sm"
                        aria-label={`Eliminar ${label.name}`}
                        className="text-muted-foreground hover:text-destructive"
                        disabled={del.isPending && del.variables === label.id}
                        onClick={() => void remove(label)}
                      >
                        <Trash2 />
                      </Button>
                    </li>
                  ),
                )}
              </ul>
            )}
            {editing === 'new' ? (
              <div className="rounded-lg border p-3">
                <LabelForm onDone={() => setEditing(null)} />
              </div>
            ) : (
              <Button variant="outline" className="w-full" onClick={() => setEditing('new')}>
                <Plus /> Nueva etiqueta
              </Button>
            )}
          </div>
        )}
      </DialogContent>
    </Dialog>
  )
}

function LabelForm({ label, onDone }: { label?: ConversationLabel; onDone: () => void }) {
  const save = useSaveLabel()
  const form = useForm<LabelValues>({
    resolver: zodResolver(labelSchema),
    defaultValues: { name: label?.name ?? '', color: label?.color ?? labelColors[0] },
  })

  const onSubmit = form.handleSubmit((values) =>
    save.mutate(
      { id: label?.id, ...values },
      {
        onSuccess: onDone,
        onError: (error) => {
          if (error instanceof ApiError) {
            for (const [field, message] of Object.entries(error.fieldErrors)) {
              if (field === 'name' || field === 'color') form.setError(field, { message })
            }
          }
        },
      },
    ),
  )

  return (
    <Form {...form}>
      <form onSubmit={onSubmit} className="space-y-3" noValidate>
        {save.error &&
          !(save.error instanceof ApiError && Object.keys(save.error.fieldErrors).length > 0) && (
            <Alert tone="destructive">{save.error.message}</Alert>
          )}
        <FormField
          control={form.control}
          name="name"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Nombre</FormLabel>
              <FormControl>
                <Input placeholder="Ej. Mayorista" autoFocus maxLength={100} {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="color"
          render={({ field }) => (
            <FormItem>
              <FormLabel id="label-color">Color</FormLabel>
              <div role="radiogroup" aria-labelledby="label-color" className="flex flex-wrap gap-2">
                {labelColors.map((color) => {
                  const selected = field.value.toLowerCase() === color
                  return (
                    <button
                      key={color}
                      type="button"
                      role="radio"
                      aria-checked={selected}
                      aria-label={`Color ${color}`}
                      onClick={() => field.onChange(color)}
                      className={cn(
                        'focus-visible:ring-ring/50 flex size-7 items-center justify-center rounded-full outline-none focus-visible:ring-[3px]',
                        selected && 'ring-foreground/40 ring-offset-background ring-2 ring-offset-2',
                      )}
                      style={{ backgroundColor: color }}
                    >
                      {selected && <Check className="size-4 text-white" aria-hidden />}
                    </button>
                  )
                })}
              </div>
              <FormMessage />
            </FormItem>
          )}
        />
        <div className="flex justify-end gap-2">
          <Button type="button" variant="ghost" size="sm" onClick={onDone}>
            Cancelar
          </Button>
          <Button type="submit" size="sm" disabled={save.isPending}>
            {save.isPending && <Loader2 className="animate-spin" />}
            {label ? 'Guardar' : 'Crear etiqueta'}
          </Button>
        </div>
      </form>
    </Form>
  )
}
