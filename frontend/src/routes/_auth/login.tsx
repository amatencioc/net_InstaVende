import { zodResolver } from '@hookform/resolvers/zod'
import { createFileRoute, Link, useNavigate } from '@tanstack/react-router'
import { Loader2 } from 'lucide-react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { Alert } from '@/components/shared/alert'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { useLogin } from '@/lib/auth/session'

const searchSchema = z.object({ redirect: z.string().optional() })

export const Route = createFileRoute('/_auth/login')({
  validateSearch: searchSchema,
  head: () => ({ meta: [{ title: 'Iniciar sesión — InstaVende' }] }),
  component: LoginPage,
})

const formSchema = z.object({
  email: z.email('Ingresa un email válido.'),
  password: z.string().min(1, 'La contraseña es obligatoria.'),
  rememberMe: z.boolean(),
})
type FormValues = z.infer<typeof formSchema>

/** Solo se permite volver a rutas internas (evita redirecciones abiertas). */
function safeRedirect(target: string | undefined): string {
  return target && target.startsWith('/') && !target.startsWith('//') ? target : '/dashboard'
}

function LoginPage() {
  const { redirect } = Route.useSearch()
  const navigate = useNavigate()
  const login = useLogin()
  const form = useForm<FormValues>({
    resolver: zodResolver(formSchema),
    defaultValues: { email: '', password: '', rememberMe: true },
  })

  const onSubmit = form.handleSubmit((values) =>
    login.mutate(values, { onSuccess: () => navigate({ to: safeRedirect(redirect) }) }),
  )

  return (
    <div className="space-y-6">
      <div className="space-y-2">
        <h1 className="text-3xl font-semibold">Bienvenido de vuelta</h1>
        <p className="text-muted-foreground">Inicia sesión para gestionar tu vendedor IA.</p>
      </div>
      {login.error && <Alert tone="destructive">{login.error.message}</Alert>}
      <Form {...form}>
        <form onSubmit={onSubmit} className="space-y-4" noValidate>
          <FormField
            control={form.control}
            name="email"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Email</FormLabel>
                <FormControl>
                  <Input type="email" autoComplete="username" placeholder="tu@negocio.com" autoFocus {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />
          <FormField
            control={form.control}
            name="password"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Contraseña</FormLabel>
                <FormControl>
                  <Input type="password" autoComplete="current-password" placeholder="••••••••" {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />
          <FormField
            control={form.control}
            name="rememberMe"
            render={({ field }) => (
              <FormItem className="flex items-center gap-2">
                <FormControl>
                  <Checkbox checked={field.value} onCheckedChange={(v) => field.onChange(v === true)} />
                </FormControl>
                <FormLabel className="font-normal">Mantener la sesión iniciada</FormLabel>
              </FormItem>
            )}
          />
          <Button type="submit" className="w-full" size="lg" disabled={login.isPending}>
            {login.isPending && <Loader2 className="animate-spin" />}
            Iniciar sesión
          </Button>
        </form>
      </Form>
      <p className="text-muted-foreground text-center text-sm">
        ¿No tienes cuenta?{' '}
        <Link to="/register" className="text-primary font-medium hover:underline">
          Crear cuenta gratis
        </Link>
      </p>
    </div>
  )
}
