import { Link, useLocation, useNavigate } from '@tanstack/react-router'
import { Bell, Building2, Lock, ShieldCheck, User, Users, type LucideIcon } from 'lucide-react'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { useIsAdmin } from '@/lib/auth/session'
import { cn } from '@/lib/utils'

interface SettingsSection {
  to: '/settings/profile' | '/settings/security' | '/settings/business' | '/settings/team' | '/settings/notifications'
  label: string
  description: string
  icon: LucideIcon
  adminOnly?: boolean
}

const settingsSections: SettingsSection[] = [
  { to: '/settings/profile', label: 'Perfil', description: 'Tu nombre, teléfono y foto', icon: User },
  { to: '/settings/security', label: 'Seguridad', description: 'Contraseña, email y sesiones', icon: ShieldCheck },
  { to: '/settings/business', label: 'Negocio', description: 'Datos, país, moneda y logo', icon: Building2, adminOnly: true },
  { to: '/settings/team', label: 'Equipo', description: 'Usuarios e invitaciones', icon: Users },
  {
    to: '/settings/notifications',
    label: 'Notificaciones',
    description: 'Avisos por email',
    icon: Bell,
    adminOnly: true,
  },
]

const ADMIN_HINT = 'Solo los administradores del negocio pueden gestionar esta sección.'

/** Navegación de configuración: lista lateral en escritorio y selector en móvil. */
export function SettingsNav() {
  const isAdmin = useIsAdmin()
  const { pathname } = useLocation()
  const navigate = useNavigate()
  const current = settingsSections.find((s) => pathname.startsWith(s.to))?.to

  return (
    <>
      {/* Móvil: selector compacto */}
      <div className="lg:hidden">
        <label htmlFor="settings-section" className="sr-only">
          Sección de configuración
        </label>
        <Select value={current} onValueChange={(to) => void navigate({ to })}>
          <SelectTrigger id="settings-section" className="w-full">
            <SelectValue placeholder="Elige una sección" />
          </SelectTrigger>
          <SelectContent>
            {settingsSections.map((s) => {
              const locked = s.adminOnly && !isAdmin
              return (
                <SelectItem key={s.to} value={s.to} disabled={locked}>
                  <s.icon aria-hidden />
                  {s.label}
                  {locked && <span className="text-muted-foreground text-xs">(solo administradores)</span>}
                </SelectItem>
              )
            })}
          </SelectContent>
        </Select>
      </div>

      {/* Escritorio: navegación lateral */}
      <nav aria-label="Secciones de configuración" className="hidden lg:block">
        <ul className="space-y-1">
          {settingsSections.map((s) => {
            const locked = s.adminOnly && !isAdmin
            const content = (
              <>
                <s.icon className="mt-0.5 size-4 shrink-0" aria-hidden />
                <span className="min-w-0 flex-1">
                  <span className="block text-sm font-medium">{s.label}</span>
                  <span className="text-muted-foreground block truncate text-xs">{s.description}</span>
                </span>
                {locked && <Lock className="mt-0.5 size-3.5 shrink-0" aria-hidden />}
              </>
            )
            return (
              <li key={s.to}>
                {locked ? (
                  <Tooltip>
                    <TooltipTrigger asChild>
                      <span
                        role="link"
                        aria-disabled="true"
                        tabIndex={0}
                        aria-label={`${s.label} (solo administradores)`}
                        className="text-muted-foreground/70 flex cursor-not-allowed items-start gap-3 rounded-lg px-3 py-2.5 outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50"
                      >
                        {content}
                      </span>
                    </TooltipTrigger>
                    <TooltipContent side="right">{ADMIN_HINT}</TooltipContent>
                  </Tooltip>
                ) : (
                  <Link
                    to={s.to}
                    className={cn(
                      'text-muted-foreground hover:bg-muted hover:text-foreground flex items-start gap-3 rounded-lg px-3 py-2.5 transition-colors outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50',
                    )}
                    activeProps={{ className: 'bg-primary/10 !text-foreground [&_svg]:text-primary' }}
                  >
                    {content}
                  </Link>
                )}
              </li>
            )
          })}
        </ul>
        {!isAdmin && (
          <p className="text-muted-foreground mt-4 flex gap-2 rounded-lg border border-dashed p-3 text-xs">
            <Lock className="mt-0.5 size-3.5 shrink-0" aria-hidden />
            Algunas secciones están reservadas a los administradores del negocio.
          </p>
        )}
      </nav>
    </>
  )
}
