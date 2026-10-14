import { createFileRoute, Link, Outlet, redirect } from '@tanstack/react-router'
import { BarChart3, MessagesSquare, ShieldCheck } from 'lucide-react'
import { Logo } from '@/components/layout/app-sidebar'
import { sessionQuery } from '@/lib/auth/session'

/** Layout de login/registro: panel de marca + formulario. Redirige si ya hay sesión. */
export const Route = createFileRoute('/_auth')({
  beforeLoad: async ({ context, location }) => {
    const session = await context.queryClient.ensureQueryData(sessionQuery)
    if (session?.business) throw redirect({ to: '/dashboard' })
    if (session && !location.pathname.startsWith('/setup-business')) throw redirect({ to: '/setup-business' })
  },
  component: AuthLayout,
})

const features = [
  { icon: MessagesSquare, title: 'Multicanal', text: 'WhatsApp, Instagram y Messenger en una sola bandeja.' },
  { icon: BarChart3, title: 'Métricas en tiempo real', text: 'Ventas, conversiones y rendimiento del bot.' },
  { icon: ShieldCheck, title: 'Seguro', text: 'Tokens cifrados y sesiones protegidas.' },
]

function AuthLayout() {
  return (
    <div className="grid min-h-svh lg:grid-cols-[1.1fr_1fr]">
      <aside className="from-primary via-primary to-brand-accent relative hidden overflow-hidden bg-gradient-to-br p-12 text-white lg:flex lg:flex-col lg:justify-between">
        <div
          aria-hidden
          className="absolute inset-0 opacity-20 [background-image:radial-gradient(circle_at_1px_1px,white_1px,transparent_0)] [background-size:24px_24px]"
        />
        <Link to="/" className="relative">
          <span className="font-display text-2xl font-bold tracking-tight">
            Insta<span className="text-white/70">Vende</span>
          </span>
        </Link>
        <div className="relative space-y-8">
          <h2 className="font-display max-w-md text-4xl leading-tight font-semibold">
            Tu vendedor con IA trabajando 24/7.
          </h2>
          <ul className="space-y-5">
            {features.map((f) => (
              <li key={f.title} className="flex gap-4">
                <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-white/15 backdrop-blur">
                  <f.icon className="size-5" aria-hidden />
                </span>
                <div>
                  <p className="font-semibold">{f.title}</p>
                  <p className="text-sm text-white/75">{f.text}</p>
                </div>
              </li>
            ))}
          </ul>
        </div>
        <p className="relative text-sm text-white/60">© {new Date().getFullYear()} InstaVende</p>
      </aside>
      <main className="flex items-center justify-center p-6 sm:p-10">
        <div className="w-full max-w-sm space-y-8">
          <Link to="/" className="lg:hidden">
            <Logo />
          </Link>
          <Outlet />
        </div>
      </main>
    </div>
  )
}
