import { useBlocker } from '@tanstack/react-router'
import { CircleDot, Loader2, Save, Undo2 } from 'lucide-react'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Button, buttonVariants } from '@/components/ui/button'
import { cn } from '@/lib/utils'

/**
 * Barra inferior pegajosa "Cambios sin guardar · Descartar · Guardar" y bloqueo de navegación
 * (incluido cerrar o recargar la pestaña) mientras haya cambios pendientes.
 */
export function UnsavedChangesBar({
  dirty,
  saving,
  onDiscard,
  formId,
  disabled,
}: {
  dirty: boolean
  saving: boolean
  onDiscard: () => void
  /** id del <form> que envía el botón Guardar. */
  formId: string
  disabled?: boolean
}) {
  const blocker = useBlocker({
    shouldBlockFn: () => dirty && !saving,
    enableBeforeUnload: () => dirty,
    withResolver: true,
  })

  return (
    <>
      <div
        className={cn(
          'pointer-events-none sticky bottom-0 z-20 -mx-4 px-4 pb-4 transition-all duration-200 sm:mx-0 sm:px-0',
          dirty ? 'translate-y-0 opacity-100' : 'translate-y-4 opacity-0',
        )}
        inert={!dirty}
      >
        <div
          role="region"
          aria-label="Cambios sin guardar"
          className={cn(
            'bg-card/95 supports-[backdrop-filter]:bg-card/80 flex flex-col gap-3 rounded-xl border p-3 shadow-lg backdrop-blur sm:flex-row sm:items-center sm:justify-between sm:px-4',
            dirty && 'pointer-events-auto',
          )}
        >
          <p className="flex items-center gap-2 text-sm font-medium">
            <CircleDot className="text-warning size-4" aria-hidden />
            Tienes cambios sin guardar
          </p>
          <div className="flex gap-2">
            <Button
              type="button"
              variant="ghost"
              onClick={onDiscard}
              disabled={!dirty || saving}
              className="flex-1 sm:flex-none"
            >
              <Undo2 /> Descartar
            </Button>
            <Button
              type="submit"
              form={formId}
              disabled={!dirty || saving || disabled}
              className="flex-1 sm:flex-none"
            >
              {saving ? <Loader2 className="animate-spin" /> : <Save />}
              Guardar cambios
            </Button>
          </div>
        </div>
      </div>

      <AlertDialog open={blocker.status === 'blocked'} onOpenChange={(open) => !open && blocker.reset?.()}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>¿Salir sin guardar?</AlertDialogTitle>
            <AlertDialogDescription>
              Tienes cambios sin guardar en esta sección. Si sales ahora se perderán.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel onClick={() => blocker.reset?.()}>Seguir editando</AlertDialogCancel>
            <AlertDialogAction
              className={buttonVariants({ variant: 'destructive' })}
              onClick={() => blocker.proceed?.()}
            >
              Descartar y salir
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  )
}
