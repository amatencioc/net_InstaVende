import { zodResolver } from '@hookform/resolvers/zod'
import { createFileRoute, Link, useNavigate } from '@tanstack/react-router'
import { Loader2 } from 'lucide-react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { Alert } from '@/components/shared/alert'
import { Button } from '@/components/ui/button'
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { ApiError } from '@/lib/api/client'
import { useRegister } from '@/lib/auth/session'

export const Route = createFileRoute('/_auth/register')({
  head: () => ({ meta: [{ title: 'Crear cuenta — InstaVende' }] }),
  component: RegisterPage,
})

const formSchema = z
  .object({
    firstName: z.string().trim().min(1, 'El nombre es obligatorio.').max(100),
    lastName: z.string().trim().min(1, 'El apellido es obligatorio.').max(100),
    businessName: z.string().trim().min(1, 'El nombre del negocio es obligatorio.').max(200),
    email: z.email('Ingresa un email válido.'),
    password: z
      .string()
      .min(8, 'La contraseña debe tener al menos 8 caracteres.')
      .regex(/\d/, 'La contraseña debe incluir al menos un número.'),
    confirmPassword: z.string(),
  })
  .refine((v) => v.password === v.confirmPassword, {
    path: ['confirmPassword'],
    message: 'Las contraseñas no coinciden.',
  })
type FormValues = z.infer<typeof formSchema>

function RegisterPage() {
  const navigate = useNavigate()
  const register = useRegister()
  const form = useForm<FormValues>({
    resolver: zodResolver(formSchema),
    defaultValues: { firstName: '', lastName: '', businessName: '', email: '', password: '', confirmPassword: '' },
  })

  const onSubmit = form.handleSubmit(({ confirmPassword: _, ...body }) =>
    register.mutate(body, {
      onSuccess: () => navigate({ to: '/dashboard' }),
      onError: (error) => {
        // Errores de validación del servidor junto a cada campo
        if (error instanceof ApiError)
          for (const [field, message] of Object.entries(error.fieldErrors))
            form.setError(field as keyof FormValues, { message })
      },
    }),
  )

  const text = (name: keyof FormValues, label: string, props: React.ComponentProps<typeof Input> = {}) => (
    <FormField
      control={form.control}
      name={name}
      render={({ field }) => (
        <FormItem>
          <FormLabel>{label}</FormLabel>
          <FormControl>
            <Input {...props} {...field} />
          </FormControl>
          <FormMessage />
        </FormItem>
      )}
    />
  )

  return (
    <div className="space-y-6">
      <div className="space-y-2">
        <h1 className="text-3xl font-semibold">Crea tu cuenta</h1>
        <p className="text-muted-foreground">Configura tu vendedor IA en minutos. 14 días gratis.</p>
      </div>
      {register.error && Object.keys((register.error as ApiError).fieldErrors ?? {}).length === 0 && (
        <Alert tone="destructive">{register.error.message}</Alert>
      )}
      <Form {...form}>
        <form onSubmit={onSubmit} className="space-y-4" noValidate>
          <div className="grid gap-4 sm:grid-cols-2">
            {text('firstName', 'Nombre', { autoComplete: 'given-name', placeholder: 'Juan', autoFocus: true })}
            {text('lastName', 'Apellido', { autoComplete: 'family-name', placeholder: 'García' })}
          </div>
          {text('businessName', 'Nombre del negocio', { autoComplete: 'organization', placeholder: 'Mi Tienda' })}
          {text('email', 'Email', { type: 'email', autoComplete: 'email', placeholder: 'tu@negocio.com' })}
          {text('password', 'Contraseña', { type: 'password', autoComplete: 'new-password', placeholder: 'Mínimo 8 caracteres' })}
          {text('confirmPassword', 'Confirmar contraseña', { type: 'password', autoComplete: 'new-password' })}
          <Button type="submit" className="w-full" size="lg" disabled={register.isPending}>
            {register.isPending && <Loader2 className="animate-spin" />}
            Crear cuenta gratis
          </Button>
        </form>
      </Form>
      <p className="text-muted-foreground text-center text-sm">
        ¿Ya tienes cuenta?{' '}
        <Link to="/login" className="text-primary font-medium hover:underline">
          Iniciar sesión
        </Link>
      </p>
    </div>
  )
}
