import { zodResolver } from '@hookform/resolvers/zod'
import { createFileRoute, redirect, useNavigate } from '@tanstack/react-router'
import { Loader2 } from 'lucide-react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { Alert } from '@/components/shared/alert'
import { Button } from '@/components/ui/button'
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { sessionQuery, useLogout, useSetupBusiness } from '@/lib/auth/session'

/** Usuario autenticado sin negocio (registro incompleto o invitado aún no vinculado). */
export const Route = createFileRoute('/_auth/setup-business')({
  beforeLoad: async ({ context }) => {
    const session = await context.queryClient.ensureQueryData(sessionQuery)
    if (!session) throw redirect({ to: '/login' })
  },
  head: () => ({ meta: [{ title: 'Configura tu negocio — InstaVende' }] }),
  component: SetupBusinessPage,
})

const formSchema = z.object({ businessName: z.string().trim().min(1, 'El nombre del negocio es obligatorio.').max(200) })

function SetupBusinessPage() {
  const navigate = useNavigate()
  const setup = useSetupBusiness()
  const logout = useLogout()
  const form = useForm({ resolver: zodResolver(formSchema), defaultValues: { businessName: '' } })

  return (
    <div className="space-y-6">
      <div className="space-y-2">
        <h1 className="text-3xl font-semibold">Configura tu negocio</h1>
        <p className="text-muted-foreground">
          Tu usuario aún no tiene un negocio. Créalo ahora o pide al administrador de tu equipo que te invite.
        </p>
      </div>
      {setup.error && <Alert tone="destructive">{setup.error.message}</Alert>}
      <Form {...form}>
        <form
          onSubmit={form.handleSubmit((v) => setup.mutate(v, { onSuccess: () => navigate({ to: '/dashboard' }) }))}
          className="space-y-4"
          noValidate
        >
          <FormField
            control={form.control}
            name="businessName"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Nombre del negocio</FormLabel>
                <FormControl>
                  <Input autoComplete="organization" placeholder="Mi Tienda" autoFocus {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />
          <Button type="submit" className="w-full" size="lg" disabled={setup.isPending}>
            {setup.isPending && <Loader2 className="animate-spin" />}
            Crear negocio
          </Button>
        </form>
      </Form>
      <Button
        variant="link"
        className="w-full"
        onClick={() => logout.mutate(undefined, { onSettled: () => navigate({ to: '/login' }) })}
      >
        Cerrar sesión
      </Button>
    </div>
  )
}
