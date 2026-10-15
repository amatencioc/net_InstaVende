import { zodResolver } from '@hookform/resolvers/zod'
import { Loader2 } from 'lucide-react'
import { useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { toast } from 'sonner'
import { useSaveKnowledge, type KnowledgeCategory, type KnowledgeEntry } from '../../api'
import { categoryMeta, KNOWLEDGE_CONTENT_MAX, KNOWLEDGE_TITLE_MAX, knowledgeCategories } from '../../meta'
import { knowledgeFormSchema, type KnowledgeFormValues } from '../../schemas'
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
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Textarea } from '@/components/ui/textarea'

export interface KnowledgeDialogState {
  entry?: KnowledgeEntry
  /** Valores iniciales al crear (sugerencias, categoría filtrada). */
  preset?: { title?: string; category?: KnowledgeCategory }
}

interface KnowledgeDialogProps {
  state: KnowledgeDialogState | null
  onOpenChange: (open: boolean) => void
}

/** Crear o editar un tema de la base de conocimiento. */
export function KnowledgeDialog({ state, onOpenChange }: KnowledgeDialogProps) {
  return (
    <Dialog open={state !== null} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-xl">
        {state && <KnowledgeForm state={state} onDone={() => onOpenChange(false)} />}
      </DialogContent>
    </Dialog>
  )
}

const fields = ['title', 'content', 'category'] as const

function KnowledgeForm({ state, onDone }: { state: KnowledgeDialogState; onDone: () => void }) {
  const { entry, preset } = state
  const save = useSaveKnowledge()
  const [serverError, setServerError] = useState<string | null>(null)
  const form = useForm<KnowledgeFormValues>({
    resolver: zodResolver(knowledgeFormSchema),
    defaultValues: {
      title: entry?.title ?? preset?.title ?? '',
      content: entry?.content ?? '',
      category: entry?.category ?? preset?.category ?? 'Otros',
    },
  })

  const onSubmit = form.handleSubmit((values) => {
    setServerError(null)
    save.mutate(
      { id: entry?.id, body: values },
      {
        onSuccess: () => {
          toast.success(entry ? 'Tema actualizado.' : 'Tema agregado. Tu vendedor ya puede usarlo.')
          onDone()
        },
        onError: (error) => setServerError(applyServerErrors(form, error, fields)),
      },
    )
  })

  const title = useWatch({ control: form.control, name: 'title' })
  const content = useWatch({ control: form.control, name: 'content' })

  return (
    <Form {...form}>
      <form onSubmit={onSubmit} className="grid gap-5" noValidate>
        <DialogHeader>
          <DialogTitle>{entry ? 'Editar tema' : 'Agregar tema'}</DialogTitle>
          <DialogDescription>
            Escribe la pregunta o tema y la respuesta que tu vendedor debe dar. Sé claro y específico.
          </DialogDescription>
        </DialogHeader>

        {serverError && <Alert tone="destructive">{serverError}</Alert>}

        <FormField
          control={form.control}
          name="category"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Categoría</FormLabel>
              <Select value={field.value} onValueChange={field.onChange}>
                <FormControl>
                  <SelectTrigger className="w-full sm:w-64">
                    <SelectValue placeholder="Selecciona una categoría" />
                  </SelectTrigger>
                </FormControl>
                <SelectContent>
                  {knowledgeCategories.map((category) => {
                    const meta = categoryMeta[category]
                    return (
                      <SelectItem key={category} value={category}>
                        <meta.icon aria-hidden />
                        {meta.label}
                      </SelectItem>
                    )
                  })}
                </SelectContent>
              </Select>
              <FormMessage />
            </FormItem>
          )}
        />

        <FormField
          control={form.control}
          name="title"
          render={({ field }) => (
            <FormItem>
              <div className="flex items-center justify-between gap-2">
                <FormLabel>Pregunta o tema</FormLabel>
                <CharCounter value={title} max={KNOWLEDGE_TITLE_MAX} />
              </div>
              <FormControl>
                <Input placeholder="¿Hacen envíos a provincia?" autoFocus maxLength={KNOWLEDGE_TITLE_MAX + 50} {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />

        <FormField
          control={form.control}
          name="content"
          render={({ field }) => (
            <FormItem>
              <div className="flex items-center justify-between gap-2">
                <FormLabel>Respuesta</FormLabel>
                <CharCounter value={content} max={KNOWLEDGE_CONTENT_MAX} />
              </div>
              <FormControl>
                <Textarea
                  rows={7}
                  className="max-h-80 min-h-36"
                  placeholder="Sí, enviamos a todo el país por agencia. El envío tarda de 2 a 4 días hábiles y el costo depende del destino."
                  {...field}
                />
              </FormControl>
              <FormDescription>Incluye datos concretos: montos, plazos, direcciones o condiciones.</FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />

        <DialogFooter>
          <Button type="button" variant="outline" onClick={onDone} disabled={save.isPending}>
            Cancelar
          </Button>
          <Button type="submit" disabled={save.isPending}>
            {save.isPending && <Loader2 className="animate-spin" aria-hidden />}
            {entry ? 'Guardar cambios' : 'Agregar tema'}
          </Button>
        </DialogFooter>
      </form>
    </Form>
  )
}
