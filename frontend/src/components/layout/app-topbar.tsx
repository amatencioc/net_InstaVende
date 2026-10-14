import { HubConnectionState } from '@microsoft/signalr'
import { Link, useNavigate } from '@tanstack/react-router'
import { LogOut, Monitor, Moon, Sun, User } from 'lucide-react'
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Separator } from '@/components/ui/separator'
import { SidebarTrigger } from '@/components/ui/sidebar'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { useLogout, useSession } from '@/lib/auth/session'
import { initials } from '@/lib/format'
import { useInboxHubState } from '@/lib/realtime/inbox-hub'
import { useTheme, type Theme } from '@/lib/theme'
import { cn } from '@/lib/utils'

function RealtimeIndicator() {
  const state = useInboxHubState()
  const connected = state === HubConnectionState.Connected
  const connecting = state === HubConnectionState.Connecting || state === HubConnectionState.Reconnecting
  const label = connected ? 'Tiempo real activo' : connecting ? 'Conectando…' : 'Sin conexión en tiempo real'
  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <span className="text-muted-foreground hidden items-center gap-2 rounded-full border px-3 py-1 text-xs sm:flex">
          <span
            className={cn(
              'size-2 rounded-full',
              connected ? 'bg-success animate-pulse' : connecting ? 'bg-warning' : 'bg-muted-foreground',
            )}
          />
          {connected ? 'En vivo' : connecting ? 'Conectando' : 'Desconectado'}
        </span>
      </TooltipTrigger>
      <TooltipContent>{label}</TooltipContent>
    </Tooltip>
  )
}

export function AppTopbar() {
  const { user, business } = useSession()
  const { theme, setTheme } = useTheme()
  const logout = useLogout()
  const navigate = useNavigate()
  const fullName = `${user.firstName} ${user.lastName}`.trim() || user.email

  return (
    <header className="bg-background/80 sticky top-0 z-20 flex h-16 shrink-0 items-center gap-2 border-b px-4 backdrop-blur-md">
      <SidebarTrigger className="-ml-1" aria-label="Mostrar u ocultar menú" />
      <Separator orientation="vertical" className="mr-2 h-4" />
      <span className="text-muted-foreground truncate text-sm">{business.name}</span>
      <div className="ml-auto flex items-center gap-2">
        <RealtimeIndicator />
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" className="h-9 gap-2 px-2" aria-label="Menú de usuario">
              <Avatar className="size-7">
                {user.avatarUrl && <AvatarImage src={user.avatarUrl} alt="" />}
                <AvatarFallback className="bg-primary/15 text-primary text-xs font-semibold">
                  {initials(fullName)}
                </AvatarFallback>
              </Avatar>
              <span className="hidden max-w-40 truncate text-sm font-medium md:inline">{fullName}</span>
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-60">
            <DropdownMenuLabel className="font-normal">
              <p className="truncate font-medium">{fullName}</p>
              <p className="text-muted-foreground truncate text-xs">{user.email}</p>
            </DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuItem asChild>
              <Link to="/settings">
                <User /> Mi cuenta
              </Link>
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuLabel className="text-muted-foreground text-xs">Tema</DropdownMenuLabel>
            <DropdownMenuRadioGroup value={theme} onValueChange={(v) => setTheme(v as Theme)}>
              <DropdownMenuRadioItem value="light">
                <Sun /> Claro
              </DropdownMenuRadioItem>
              <DropdownMenuRadioItem value="dark">
                <Moon /> Oscuro
              </DropdownMenuRadioItem>
              <DropdownMenuRadioItem value="system">
                <Monitor /> Sistema
              </DropdownMenuRadioItem>
            </DropdownMenuRadioGroup>
            <DropdownMenuSeparator />
            <DropdownMenuItem
              variant="destructive"
              onSelect={() => logout.mutate(undefined, { onSettled: () => navigate({ to: '/login' }) })}
            >
              <LogOut /> Cerrar sesión
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </header>
  )
}
